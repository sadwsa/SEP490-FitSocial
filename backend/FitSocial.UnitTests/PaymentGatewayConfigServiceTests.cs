using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.PaymentGateway;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PaymentGatewayConfigServiceTests
{
    private readonly Mock<IPaymentGatewayConfigRepository> _configRepoMock;
    private readonly Mock<IEncryptionService> _encryptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly PaymentGatewayConfigService _service;

    public PaymentGatewayConfigServiceTests()
    {
        _configRepoMock = new Mock<IPaymentGatewayConfigRepository>();
        _encryptionMock = new Mock<IEncryptionService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new PaymentGatewayConfigService(_configRepoMock.Object, _encryptionMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CreateConfigAsync_ShouldEncryptSensitiveKeysAndSave()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var dto = new CreatePaymentGatewayConfigDto
        {
            GatewayName = "PAYOS",
            ClientId = "test-client-id-12345",
            ApiKey = "api-secret-key-12345678",
            ChecksumKey = "checksum-secret-key-87654321",
            WebhookUrl = "https://api.fitsocial.com/webhooks/payos",
            IsActive = true
        };

        var encApiKey = Encoding.UTF8.GetBytes("encrypted_api_key_bytes");
        var encChecksumKey = Encoding.UTF8.GetBytes("encrypted_checksum_bytes");

        _encryptionMock.Setup(e => e.Encrypt(dto.ApiKey, false)).Returns(encApiKey);
        _encryptionMock.Setup(e => e.Encrypt(dto.ChecksumKey, false)).Returns(encChecksumKey);
        _encryptionMock.Setup(e => e.Decrypt(encApiKey)).Returns(dto.ApiKey);
        _encryptionMock.Setup(e => e.Decrypt(encChecksumKey)).Returns(dto.ChecksumKey);

        _configRepoMock.Setup(r => r.GetAllConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentGatewayConfig>());
        _configRepoMock.Setup(r => r.AddAsync(It.IsAny<PaymentGatewayConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateConfigAsync(adminId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("PAYOS", result.Data.GatewayName);
        Assert.Equal("test-client-id-12345", result.Data.ClientId);
        Assert.Contains("...", result.Data.ApiKeyMasked!); // Key is masked
        Assert.Contains("...", result.Data.ChecksumKeyMasked!); // Key is masked

        _configRepoMock.Verify(r => r.AddAsync(It.Is<PaymentGatewayConfig>(c =>
            c.EncryptedApiKey == encApiKey &&
            c.EncryptedChecksumKey == encChecksumKey &&
            c.UpdatedBy == adminId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
