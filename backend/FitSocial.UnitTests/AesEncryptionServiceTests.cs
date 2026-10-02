using FitSocial.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text;
using Xunit;

namespace FitSocial.UnitTests;

public class AesEncryptionServiceTests
{
    private readonly AesEncryptionService _encryptionService;

    public AesEncryptionServiceTests()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Encryption:Key"]).Returns("TestEncryptionKeyForFitSocial123!");
        _encryptionService = new AesEncryptionService(mockConfig.Object);
    }

    [Fact]
    public void EncryptAndDecrypt_ShouldRestoreOriginalPlainText()
    {
        // Arrange
        var originalText = "012345678901";

        // Act
        var encryptedBytes = _encryptionService.Encrypt(originalText, deterministic: false);
        var decryptedText = _encryptionService.Decrypt(encryptedBytes);

        // Assert
        Assert.NotNull(encryptedBytes);
        Assert.NotEmpty(encryptedBytes);
        Assert.Equal(originalText, decryptedText);
    }

    [Fact]
    public void DeterministicEncrypt_ShouldProduceSameCiphertextForSameInput()
    {
        // Arrange
        var originalText = "079201001234";

        // Act
        var cipher1 = _encryptionService.Encrypt(originalText, deterministic: true);
        var cipher2 = _encryptionService.Encrypt(originalText, deterministic: true);

        // Assert
        Assert.Equal(cipher1, cipher2);
        Assert.Equal(originalText, _encryptionService.Decrypt(cipher1));
    }

    [Fact]
    public void Decrypt_WithInvalidOrEmptyBytes_ShouldReturnNull()
    {
        // Act & Assert
        Assert.Null(_encryptionService.Decrypt(null));
        Assert.Null(_encryptionService.Decrypt(System.Array.Empty<byte>()));
        Assert.Null(_encryptionService.Decrypt(new byte[] { 1, 2, 3 })); // Invalid short length
    }
}
