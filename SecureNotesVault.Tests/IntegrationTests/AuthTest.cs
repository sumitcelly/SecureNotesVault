using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SecureNotesVault.Core;
using SecureNotesVault.Core.Services;
using Xunit;

namespace SecureNotesVault.Tests.IntegrationTests;

public class AuthIntegrationTests
{
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    public AuthIntegrationTests()
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

        _authService = new AuthService(_context, mockConfig.Object);
    }

    [Fact]
    public async Task RegisterAsync_ShouldSuccessfully_HashPasswordAndPersistUser()
    {
        // Arrange
        string username = "testuser";
        string rawPassword = "SecurePassword123!";

        // Act
        var user = await _authService.RegisterAsync(username, rawPassword);

        // Assert
        Assert.NotNull(user);
        Assert.Equal(username, user.Username);
        
        // Critically confirm the raw text password is NOT stored anywhere in the data row
        Assert.NotEqual(rawPassword, user.PasswordHash);

        // Verify the database actually committed and tracks the record
        var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        Assert.NotNull(dbUser);
    }

    [Fact]
    public async Task RegisterAsync_ShouldReturnNull_IfUsernameAlreadyExists()
    {
        // Arrange
        string duplicateUsername = "existinguser";
        await _authService.RegisterAsync(duplicateUsername, "Password123!");

        // Act
        var secondAttempt = await _authService.RegisterAsync(duplicateUsername, "DifferentPassword123!");

        // Assert
        Assert.Null(secondAttempt);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnSignedJwt_OnValidCredentials()
    {
        // Arrange
        string username = "loginuser";
        string password = "ValidPassword123!";
        await _authService.RegisterAsync(username, password);

        // Act
        string? jwtToken = await _authService.LoginAsync(username, password);

        // Assert
        Assert.NotNull(jwtToken);
        Assert.NotEmpty(jwtToken);
        
        // A standard valid JWT contains two dot breaks (Header.Payload.Signature)
        Assert.Equal(2, jwtToken.Count(c => c == '.'));
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_OnInvalidPassword()
    {
        // Arrange
        string username = "secureuser";
        await _authService.RegisterAsync(username, "CorrectPassword123!");

        // Act
        string? jwtToken = await _authService.LoginAsync(username, "WrongPassword123!");

        // Assert
        Assert.Null(jwtToken);
    }
}
