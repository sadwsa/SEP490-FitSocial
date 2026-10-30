using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Mock<IAuditLogService> _auditLogMock;
    private readonly PaymentGatewayConfigService _service;

    public PaymentGatewayConfigServiceTests()
    {
        _configRepoMock = new Mock<IPaymentGatewayConfigRepository>();
        _encryptionMock = new Mock<IEncryptionService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _auditLogMock = new Mock<IAuditLogService>();
        _auditLogMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        _service = new PaymentGatewayConfigService(
            _configRepoMock.Object, _encryptionMock.Object, _unitOfWorkMock.Object, _auditLogMock.Object);
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

    // ---------- Audit log (Add / Edit / Activate / Deactivate payment account) ----------

    private static PaymentGatewayConfig ExistingConfig(bool isActive = true, bool isPayout = false) => new()
    {
        GatewayId = Guid.NewGuid(),
        GatewayName = "PAYOS",
        ClientId = "client-12345",
        EncryptedApiKey = Encoding.UTF8.GetBytes("enc-api-key"),
        EncryptedChecksumKey = Encoding.UTF8.GetBytes("enc-checksum"),
        WebhookUrl = "https://api.fitsocial.com/webhooks/payos",
        IsActive = isActive,
        IsPayout = isPayout,
        UpdatedAt = DateTime.UtcNow
    };

    private void VerifyAuditLogged(string action, Guid gatewayId)
    {
        _auditLogMock.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(),
            action,
            "PaymentGatewayConfig",
            gatewayId.ToString(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateConfigAsync_ShouldWriteAuditLog_WithoutLeakingSecrets()
    {
        var dto = new CreatePaymentGatewayConfigDto
        {
            GatewayName = "PAYOS",
            ClientId = "client-12345",
            ApiKey = "super-secret-api-key",
            ChecksumKey = "super-secret-checksum",
            WebhookUrl = "https://api.fitsocial.com/webhooks/payos",
            IsActive = true
        };

        _encryptionMock.Setup(e => e.Encrypt(It.IsAny<string>(), false)).Returns(Encoding.UTF8.GetBytes("enc"));
        _encryptionMock.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("x");
        _configRepoMock.Setup(r => r.GetAllConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentGatewayConfig>());
        _configRepoMock.Setup(r => r.AddAsync(It.IsAny<PaymentGatewayConfig>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.CreateConfigAsync(Guid.NewGuid(), dto);

        Assert.True(result.Success);
        VerifyAuditLogged("ADD_PAYMENT_ACCOUNT", result.Data!.GatewayId);

        // Secrets must never reach the AuditLogs table.
        var auditCall = _auditLogMock.Invocations
            .Single(i => i.Method.Name == nameof(IAuditLogService.LogAsync) && i.Arguments.Count == 10);
        var newValue = System.Text.Json.JsonSerializer.Serialize(auditCall.Arguments[6]);
        Assert.DoesNotContain("super-secret-api-key", newValue);
        Assert.DoesNotContain("super-secret-checksum", newValue);
    }

    [Fact]
    public async Task UpdateConfigAsync_ShouldWriteAuditLogWithOldAndNewValue()
    {
        var config = ExistingConfig();
        _configRepoMock.Setup(r => r.GetByIdAsync(config.GatewayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _configRepoMock.Setup(r => r.GetAllConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentGatewayConfig> { config });
        _encryptionMock.Setup(e => e.Encrypt(It.IsAny<string>(), false)).Returns(Encoding.UTF8.GetBytes("new-enc"));
        _encryptionMock.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("x");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdateConfigAsync(
            config.GatewayId, Guid.NewGuid(), new UpdatePaymentGatewayConfigDto { GatewayName = "PAYOS v2" });

        Assert.True(result.Success);
        Assert.Equal("PAYOS v2", config.GatewayName);
        VerifyAuditLogged("EDIT_PAYMENT_ACCOUNT", config.GatewayId);
    }

    [Fact]
    public async Task ActivateConfigAsync_ShouldWriteAuditLog()
    {
        var config = ExistingConfig(isActive: false);
        _configRepoMock.Setup(r => r.GetByIdAsync(config.GatewayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _configRepoMock.Setup(r => r.GetAllConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentGatewayConfig> { config });
        _encryptionMock.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("x");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ActivateConfigAsync(config.GatewayId);

        Assert.True(result.Success);
        Assert.True(config.IsActive);
        VerifyAuditLogged("ACTIVATE_PAYMENT_ACCOUNT", config.GatewayId);
    }

    [Fact]
    public async Task DeactivateConfigAsync_ShouldWriteAuditLog()
    {
        var config = ExistingConfig(isActive: true);
        _configRepoMock.Setup(r => r.GetByIdAsync(config.GatewayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _encryptionMock.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("x");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.DeactivateConfigAsync(config.GatewayId);

        Assert.True(result.Success);
        Assert.False(config.IsActive);
        VerifyAuditLogged("DEACTIVATE_PAYMENT_ACCOUNT", config.GatewayId);
    }

    [Fact]
    public async Task ActivateConfigAsync_WhenConfigNotFound_ShouldNotWriteAuditLog()
    {
        var id = Guid.NewGuid();
        _configRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentGatewayConfig?)null);

        await Assert.ThrowsAsync<FitSocial.Application.Exceptions.NotFoundException>(
            () => _service.ActivateConfigAsync(id));

        _auditLogMock.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateConfigAsync_WhenAuditFails_ShouldStillSucceed()
    {
        var config = ExistingConfig(isActive: true);
        _configRepoMock.Setup(r => r.GetByIdAsync(config.GatewayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _encryptionMock.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("x");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _auditLogMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit table down"));

        var result = await _service.DeactivateConfigAsync(config.GatewayId);

        Assert.True(result.Success);
        Assert.False(config.IsActive);
    }
}
