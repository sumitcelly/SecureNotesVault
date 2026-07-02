using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using SecureNotesVault.Core.Services;

namespace SecureNotesVault.Tests.UnitTests;

public class EncryptionTests
{
    private readonly AesGcmEncryptionService _service;

    public EncryptionTests()
    {
        // Mock configuration setup using a valid 32-byte key fallback
        var mockConfig = new Mock<IConfiguration>();
        _service = new AesGcmEncryptionService(mockConfig.Object);
    }

    [Fact]
    public void Encrypt_ShouldObfuscatePlainText()
    {
        // Arrange
        string sampleSecretText = "TopSecretDoDData";

        // Act
        string cipherText = _service.Encrypt(sampleSecretText);

        // Assert
        Assert.NotEqual(sampleSecretText, cipherText);
        Assert.DoesNotContain("TopSecret", cipherText);
    }

    [Fact]
    public void Decrypt_ShouldRestoreOriginalTextPerfectlY()
    {
        // Arrange
        string expectedText = "LaunchCodes12345";
        string cipherText = _service.Encrypt(expectedText);

        // Act
        string decryptedText = _service.Decrypt(cipherText);

        // Assert
        Assert.Equal(expectedText, decryptedText);
    }
}
