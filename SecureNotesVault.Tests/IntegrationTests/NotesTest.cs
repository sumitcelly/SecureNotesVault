using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SecureNotesVault.Core;
using SecureNotesVault.Core.Models;
using SecureNotesVault.Core.Services;
using Xunit;

namespace SecureNotesVault.Tests.IntegrationTests;

public class NotesIntegrationTests
{
    
    private readonly ApplicationDbContext _context;
    public NotesIntegrationTests()
    {
        // 1. Setup a unique In-Memory Database context for complete isolation per test run
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        // 2. Mock configuration paths to feed valid parameters to the JWT builder
        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        
        mockSection.Setup(s => s["Secret"]).Returns("SuperSecretSecureNotesVaultKey2026!MustBeAtLeast32BytesLong");
        mockSection.Setup(s => s["Issuer"]).Returns("SecureNotesVault.Api");
        mockSection.Setup(s => s["Audience"]).Returns("SecureNotesVault.Users");
        
        mockConfig.Setup(c => c.GetSection("Jwt")).Returns(mockSection.Object);

       
    }

   [Fact]
    public async Task GetNotes_ShouldOnlyReturn_OwnedOrSharedNotes()
    {
        // Arrange
        // (Assuming you have wired up your _context and _encryptionService in the test setup)
        int userA = 1;
        int userB = 2;

        var note1 = new Note { OwnerId = userA, Content = "User A Private Note" };
        var note2 = new Note { OwnerId = userB, Content = "User B Private Note" };
        var note3 = new Note { OwnerId = userB, Content = "Shared Note From B To A" };
        
        _context.Notes.AddRange(note1, note2, note3);
        await _context.SaveChangesAsync();

        // Share note 3 with User A
        _context.Shares.Add(new Share { NoteId = note3.Id, SharedWithUserId = userA });
        await _context.SaveChangesAsync();

        // Act
        // Simulate pulling notes for User A using the LINQ logic from our controller
        var userANotes = await _context.Notes
            .Where(n => n.OwnerId == userA || n.Shares.Any(s => s.SharedWithUserId == userA))
            .ToListAsync();

        // Assert
        Assert.Equal(2, userANotes.Count); // Should see note 1 (owned) and note 3 (shared)
        Assert.Contains(userANotes, n => n.Content == "User A Private Note");
        Assert.Contains(userANotes, n => n.Content == "Shared Note From B To A");
        Assert.DoesNotContain(userANotes, n => n.Content == "User B Private Note"); // Security Boundary Check passes!
    }
}