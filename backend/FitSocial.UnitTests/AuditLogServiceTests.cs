using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.AuditLogs;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ILogger<AuditLogService>> _mockLogger;
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _mockRepo = new Mock<IAuditLogRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<AuditLogService>>();
        _service = new AuditLogService(_mockRepo.Object, _mockUow.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task LogAsync_SuccessfullyCreatesAuditLog_AndCallsUnitOfWork()
    {
        // Arrange
        var actorId = Guid.NewGuid();
        AuditLog? capturedLog = null;

        _mockRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((l, ct) => capturedLog = l)
            .Returns(Task.CompletedTask);

        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.LogAsync(
            actorAccountId: actorId,
            action: "APPROVE_COACH_CERTIFICATE",
            entityType: "CoachCertificate",
            entityId: "123",
            description: "Coach certificate approved",
            oldValue: new { status = "PENDING" },
            newValue: new { status = "APPROVED" },
            ipAddress: "127.0.0.1",
            actorRole: "STAFF");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(actorId, result.ActorAccountId);
        Assert.Equal("APPROVE_COACH_CERTIFICATE", result.Action);
        Assert.Equal("CoachCertificate", result.EntityType);
        Assert.Equal("123", result.EntityId);
        Assert.Equal("127.0.0.1", result.Ipaddress);
        Assert.Contains("PENDING", result.OldValue);
        Assert.Contains("APPROVED", result.NewValue);
        Assert.Contains("Coach certificate approved", result.NewValue);
        Assert.Contains("STAFF", result.NewValue);

        _mockRepo.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogAsync_MasksSensitiveInformation_InJsonValues()
    {
        // Arrange
        AuditLog? capturedLog = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((l, ct) => capturedLog = l)
            .Returns(Task.CompletedTask);

        // Act
        await _service.LogAsync(
            actorAccountId: Guid.NewGuid(),
            action: "UPDATE_USER",
            entityType: "User",
            entityId: "456",
            description: "Updated user security details",
            oldValue: new { password = "SuperSecretPassword123!", token = "jwt_token_abc" },
            newValue: new { password = "NewSecretPassword456!", token = "jwt_token_xyz", email = "test@user.com" });

        // Assert
        Assert.NotNull(capturedLog);
        Assert.DoesNotContain("SuperSecretPassword123!", capturedLog.OldValue);
        Assert.DoesNotContain("NewSecretPassword456!", capturedLog.NewValue);
        Assert.DoesNotContain("jwt_token_abc", capturedLog.OldValue);
        Assert.DoesNotContain("jwt_token_xyz", capturedLog.NewValue);
        Assert.Contains("[REDACTED]", capturedLog.OldValue);
        Assert.Contains("[REDACTED]", capturedLog.NewValue);
        Assert.Contains("test@user.com", capturedLog.NewValue);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ReturnsPagedResult()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            new()
            {
                AuditLogId = 1,
                Action = "LOCK_USER",
                EntityType = "User",
                EntityId = "user-1",
                CreatedAt = DateTime.UtcNow,
                ActorAccount = new User { UserId = Guid.NewGuid(), FullName = "Admin User", Email = "admin@fitsocial.com", RoleCode = "ADMIN" }
            }
        };

        _mockRepo.Setup(r => r.GetPagedAuditLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            1,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 1));

        // Act
        var result = await _service.GetAuditLogsAsync(new GetAuditLogsQueryDto { PageNumber = 1, PageSize = 10 });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("LOCK_USER", result.Data.Items[0].Action);
        Assert.Equal("Admin User", result.Data.Items[0].ActorName);
        Assert.Equal("ADMIN", result.Data.Items[0].ActorRole);
    }

    [Fact]
    public async Task GetAuditLogByIdAsync_ReturnsNotFound_WhenLogDoesNotExist()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdWithActorAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog?)null);

        // Act
        var result = await _service.GetAuditLogByIdAsync(999);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAuditLogByIdAsync_ReturnsCorrectLog_WhenLogExists()
    {
        // Arrange
        var log = new AuditLog
        {
            AuditLogId = 10,
            Action = "DELETE_POST",
            EntityType = "Post",
            EntityId = "post-100",
            CreatedAt = DateTime.UtcNow,
            NewValue = "{\"description\":\"Post deleted by staff\",\"actorRole\":\"STAFF\"}",
            ActorAccount = new User { UserId = Guid.NewGuid(), FullName = "Staff User", Email = "staff@fitsocial.com", RoleCode = "STAFF" }
        };

        _mockRepo.Setup(r => r.GetByIdWithActorAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        // Act
        var result = await _service.GetAuditLogByIdAsync(10);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.AuditLogId);
        Assert.Equal("DELETE_POST", result.Data.Action);
        Assert.Equal("Post deleted by staff", result.Data.Description);
        Assert.Equal("Staff User", result.Data.ActorName);
        Assert.Equal("STAFF", result.Data.ActorRole);
    }

    [Fact]
    public async Task LogAsync_AutoResolvesActorFromCurrentUserContext_WhenOmitted()
    {
        // Arrange
        var contextUserId = Guid.NewGuid();
        var mockUserContext = new Mock<FitSocial.Application.Interfaces.ICurrentUserContext>();
        mockUserContext.Setup(c => c.UserId).Returns(contextUserId);
        mockUserContext.Setup(c => c.Role).Returns("STAFF");
        mockUserContext.Setup(c => c.IpAddress).Returns("192.168.1.100");

        var serviceWithContext = new AuditLogService(
            _mockRepo.Object,
            _mockUow.Object,
            _mockLogger.Object,
            mockUserContext.Object);

        AuditLog? capturedLog = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((l, ct) => capturedLog = l)
            .Returns(Task.CompletedTask);

        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act - call LogAsync without passing actorAccountId, actorRole, ipAddress
        var result = await serviceWithContext.LogAsync(
            actorAccountId: null,
            action: "UPDATE_STAFF",
            entityType: "Staff",
            entityId: "staff-123",
            description: "Staff profile updated");

        // Assert
        Assert.NotNull(capturedLog);
        Assert.Equal(contextUserId, capturedLog.ActorAccountId);
        Assert.Equal("192.168.1.100", capturedLog.Ipaddress);
        Assert.Contains("STAFF", capturedLog.NewValue);
        Assert.Contains("Staff profile updated", capturedLog.NewValue);
    }
}
