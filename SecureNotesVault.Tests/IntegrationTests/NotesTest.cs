using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SecureNotesVault.Api.Controllers;
using SecureNotesVault.Core;
using SecureNotesVault.Core.Models;
using SecureNotesVault.Core.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace SecureNotesVault.Tests.IntegrationTests;

public class NotesIntegrationTests
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IEncryptionService> _mockEncryptionService;

    public NotesIntegrationTests()
    {
        // Setup an isolated In-Memory database for this test pass
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockEncryptionService = new Mock<IEncryptionService>();

        // Set up mock encryption behavior
        _mockEncryptionService.Setup(s => s.Encrypt(It.IsAny<string>())).Returns((string t) => "Encrypted_" + t);
        _mockEncryptionService.Setup(s => s.Decrypt(It.IsAny<string>())).Returns((string t) => t.Replace("Encrypted_", ""));
    }

    // Helper method to mock the authenticated user context inside the controller
    private NotesController CreateControllerWithUser(int userId)
    {
        var controller = new NotesController(_context, _mockEncryptionService.Object);
        
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        return controller;
    }

    [Fact]
    public async Task CreateNote_ShouldSaveEncryptedNoteToDatabase()
    {
        // Arrange
        int ownerId = 1;
        var controller = CreateControllerWithUser(ownerId);
        var request = new NoteRequest { Content = "TopSecretLaunchCodes" };

        // Act
        var result = await controller.CreateNote(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result);
        
        var savedNote = await _context.Notes.FirstOrDefaultAsync(n => n.OwnerId == ownerId);
        Assert.NotNull(savedNote);
        // Verify application-layer encryption was called before saving
        Assert.Equal("Encrypted_TopSecretLaunchCodes", savedNote.Content);
    }

    [Fact]
    public async Task GetNoteById_ShouldAllowOwnerToReadAndDecrypt()
    {
        // Arrange
        int ownerId = 42;
        var note = new Note { OwnerId = ownerId, Content = "Encrypted_MyPrivateThoughts" };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(ownerId);

        // Act
        var result = await controller.GetNoteById(note.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? data = okResult.Value;
        Assert.NotNull(data);
        // Verify transparent decryption occurred on read
        Assert.Equal("MyPrivateThoughts", data?.GetType().GetProperty("Content")?.GetValue(data, null));
    }

    [Fact]
    public async Task GetNoteById_ShouldReturnForbidden_ForUnauthorizedUser()
    {
        // Arrange
        int ownerId = 1;
        int hackerId = 99;
        
        var note = new Note { OwnerId = ownerId, Content = "Encrypted_OwnerSecrets" };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        // Acting user is the hacker, not the owner
        var controller = CreateControllerWithUser(hackerId);

        // Act
        var result = await controller.GetNoteById(note.Id);

        // Assert
        // Verify security boundary completely blocks access
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetNoteById_ShouldAllowRecipientToReadSharedNoteAsReadOnly()
    {
        // Arrange
        int ownerId = 1;
        int recipientId = 2;

        var note = new Note { OwnerId = ownerId, Content = "Encrypted_SharedIntel" };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        // Grant explicit permission via the Share table
        _context.Shares.Add(new Share { NoteId = note.Id, SharedWithUserId = recipientId });
        await _context.SaveChangesAsync();

        var controller = CreateControllerWithUser(recipientId);

        // Act
        var result = await controller.GetNoteById(note.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? data = okResult.Value;
        Assert.NotNull(data);
        Assert.Equal("SharedIntel", data?.GetType().GetProperty("Content")?.GetValue(data, null));
        Assert.True((bool?)data?.GetType().GetProperty("IsReadOnly")?.GetValue(data, null));
    }
}
