using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.SystemLogs;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class SystemLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _repoMock;
    private readonly Mock<ILogger<SystemLogService>> _loggerMock;
    private readonly SystemLogService _service;

    public SystemLogServiceTests()
    {
        _repoMock = new Mock<IAuditLogRepository>();
        _loggerMock = new Mock<ILogger<SystemLogService>>();
        _service = new SystemLogService(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetSystemLogsAsync_ShouldReturnPagedLogsSuccessfully()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            new AuditLog
            {
                AuditLogId = 1,
                Action = "UPDATE_PRICE",
                EntityType = "CoachSubscriptionPlan",
                EntityId = "plan-123",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                OldValue = "{\"amount\": 100000}",
                NewValue = "{\"amount\": 200000}",
                ActorAccount = new User { FullName = "Admin User", Email = "admin@fitsocial.com" }
            },
            new AuditLog
            {
                AuditLogId = 2,
                Action = "APPROVE_REFUND_REQUEST",
                EntityType = "RefundRequest",
                EntityId = "ref-456",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ActorAccount = new User { FullName = "Staff Member", Email = "staff@fitsocial.com" }
            }
        };

        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            1,
            20,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 2));

        var query = new GetSystemLogsQueryDto { PageNumber = 1, PageSize = 20 };

        // Act
        var result = await _service.GetSystemLogsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal("LOG-000001", result.Data.Items[0].LogIdDisplay);
        Assert.Equal("Information", result.Data.Items[0].Level);
        Assert.Equal("Admin User", result.Data.Items[0].ActorName);
    }

    [Fact]
    public async Task GetLogDetailAsync_ExistingLog_ShouldReturnLogDetail()
    {
        // Arrange
        var log = new AuditLog
        {
            AuditLogId = 100,
            Action = "SYSTEM_ERROR",
            EntityType = "System",
            EntityId = "/api/orders/checkout",
            CreatedAt = DateTime.UtcNow,
            Ipaddress = "127.0.0.1",
            NewValue = "{\"errorMessage\": \"Database connection timed out\"}",
            ActorAccount = null
        };

        _repoMock.Setup(r => r.GetByIdWithActorAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        // Act
        var result = await _service.GetLogDetailAsync(100);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.LogId);
        Assert.Equal("Error", result.Data.Level);
        Assert.Equal("System", result.Data.ActorName);
        Assert.Contains("error", result.Data.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetLogDetailAsync_NonExistentLog_ShouldReturnFailure()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByIdWithActorAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog?)null);

        // Act
        var result = await _service.GetLogDetailAsync(999);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SYSTEM_ERROR", "Error")]
    [InlineData("UNHANDLED_EXCEPTION", "Error")]
    [InlineData("PAYMENT_FAILED", "Error")]
    [InlineData("CRITICAL_SECURITY_ALERT", "Error")]
    [InlineData("REJECT_REFUND_REQUEST", "Warning")]
    [InlineData("LOCK_ACCOUNT", "Warning")]
    [InlineData("CANCEL_SUBSCRIPTION", "Warning")]
    [InlineData("DELETE_PACKAGE", "Warning")]
    [InlineData("APPROVE_REFUND_REQUEST", "Information")]
    [InlineData("UPDATE_PRICE", "Information")]
    [InlineData("PROCESS_PAYOUT", "Information")]
    public void DetermineLogLevel_ShouldAccuratelyCategorizeLogLevel(string action, string expectedLevel)
    {
        var level = SystemLogService.DetermineLogLevel(action);
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void Sanitize_ShouldMaskSensitiveCredentialsAndTokens()
    {
        // Arrange
        var rawJson = "{\"username\": \"admin\", \"password\": \"SuperSecret123!\", \"token\": \"eyJhbGciOiJIUzI1NiJ9.test\", \"apiKey\": \"ak_live_xyz987\"}";

        // Act
        var sanitized = SystemLogService.Sanitize(rawJson);

        // Assert
        Assert.NotNull(sanitized);
        Assert.DoesNotContain("SuperSecret123!", sanitized);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9.test", sanitized);
        Assert.DoesNotContain("ak_live_xyz987", sanitized);
        Assert.Contains("\"password\": \"***REDACTED***\"", sanitized);
        Assert.Contains("\"token\": \"***REDACTED***\"", sanitized);
        Assert.Contains("\"apiKey\": \"***REDACTED***\"", sanitized);
    }

    [Fact]
    public void Sanitize_ShouldMaskConnectionStringAndBearerTokens()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=fitsocial;Username=postgres;Password=SecretDbPassword123;";
        var bearerToken = "Authorization: Bearer my_jwt_token_123456789";

        // Act
        var sanitizedConn = SystemLogService.Sanitize(connectionString);
        var sanitizedBearer = SystemLogService.Sanitize(bearerToken);

        // Assert
        Assert.NotNull(sanitizedConn);
        Assert.DoesNotContain("SecretDbPassword123", sanitizedConn);
        Assert.Contains("Password=***REDACTED***", sanitizedConn);

        Assert.NotNull(sanitizedBearer);
        Assert.DoesNotContain("my_jwt_token_123456789", sanitizedBearer);
        Assert.Contains("Bearer ***REDACTED***", sanitizedBearer);
    }

    [Fact]
    public async Task GetSystemLogsAsync_WhenPageSizeExceeds100_ClampsPageSizeTo100()
    {
        // Arrange (Boundary case: PageSize > 100)
        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            1,
            200,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((new System.Collections.Generic.List<AuditLog>(), 0));

        var query = new GetSystemLogsQueryDto { PageNumber = 1, PageSize = 200 };

        // Act
        var result = await _service.GetSystemLogsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.PageSize); // Clamped to max 100
    }

    [Fact]
    public async Task GetSystemLogsAsync_WhenPageNumberLessThan1_NormalizesToOne()
    {
        // Arrange (Boundary case: PageNumber < 1)
        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            -5,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((new System.Collections.Generic.List<AuditLog>(), 0));

        var query = new GetSystemLogsQueryDto { PageNumber = -5, PageSize = 10 };

        // Act
        var result = await _service.GetSystemLogsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.PageNumber); // Normalized to min 1
    }

    [Fact]
    public async Task GetSystemLogsAsync_WhenNoLogsFound_ReturnsEmptyPagedResult()
    {
        // Arrange (Boundary case: 0 items)
        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((new System.Collections.Generic.List<AuditLog>(), 0));

        var query = new GetSystemLogsQueryDto { PageNumber = 1, PageSize = 10 };

        // Act
        var result = await _service.GetSystemLogsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetSystemLogsAsync_WhenRepositoryThrows_ReturnsFailResult()
    {
        // Arrange (Exception case)
        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to read audit logs from database"));

        // Act
        var result = await _service.GetSystemLogsAsync(new GetSystemLogsQueryDto());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Failed to load system logs", result.Message);
    }

    [Fact]
    public async Task GetSystemLogsAsync_ExcludesTraineeAndCoachActorLogs_OnlyReturnsAdminAndStaff()
    {
        // Arrange
        var logs = new List<AuditLog>
        {
            new AuditLog
            {
                AuditLogId = 10,
                Action = "LOCK_USER",
                EntityType = "User",
                EntityId = "trainee-1",
                CreatedAt = DateTime.UtcNow,
                OldValue = "{\"isLocked\": false, \"status\": \"ACTIVE\", \"targetName\": \"Trainee One\"}",
                NewValue = "{\"isLocked\": true, \"status\": \"LOCKED\", \"targetName\": \"Trainee One\"}",
                ActorAccount = new User { FullName = "System Admin", Email = "admin@fitsocial.com", RoleCode = "ADMIN" }
            },
            new AuditLog
            {
                AuditLogId = 11,
                Action = "APPROVE_COACH_APPLICATION",
                EntityType = "CoachProfile",
                EntityId = "coach-1",
                CreatedAt = DateTime.UtcNow,
                ActorAccount = new User { FullName = "Operations Staff", Email = "staff@fitsocial.com", RoleCode = "STAFF" }
            },
            new AuditLog
            {
                AuditLogId = 12,
                Action = "UPDATE_PROFILE",
                EntityType = "User",
                EntityId = "trainee-2",
                CreatedAt = DateTime.UtcNow,
                ActorAccount = new User { FullName = "Trainee Actor", Email = "trainee@fitsocial.com", RoleCode = "TRAINEE" }
            },
            new AuditLog
            {
                AuditLogId = 13,
                Action = "UPDATE_PROFILE",
                EntityType = "User",
                EntityId = "coach-2",
                CreatedAt = DateTime.UtcNow,
                ActorAccount = new User { FullName = "Coach Actor", Email = "coach@fitsocial.com", RoleCode = "COACH" }
            }
        };

        _repoMock.Setup(r => r.GetPagedLogsAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            1,
            20,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 4));

        // Act
        var result = await _service.GetSystemLogsAsync(new GetSystemLogsQueryDto { PageNumber = 1, PageSize = 20 });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.All(result.Data.Items, item => Assert.True(item.ActorRole == "ADMIN" || item.ActorRole == "STAFF"));
        Assert.Equal("LOCK", result.Data.Items[0].ActionType);
        Assert.Equal("Trainee One", result.Data.Items[0].TargetDisplayName);
    }

    [Fact]
    public async Task GetSystemLogsSummaryAsync_ShouldReturnSummaryCounts()
    {
        // Arrange
        _repoMock.Setup(r => r.GetLogsSummaryAsync(
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((120, 45, 75, 4));

        // Act
        var result = await _service.GetSystemLogsSummaryAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(120, result.Data.TotalActivities);
        Assert.Equal(45, result.Data.AdminActivities);
        Assert.Equal(75, result.Data.StaffActivities);
        Assert.Equal(4, result.Data.FailedActivities);
    }

    [Theory]
    [InlineData("CREATE_STAFF", "CREATE")]
    [InlineData("CREATE_LOCATION", "CREATE")]
    [InlineData("UPDATE_LOCATION", "UPDATE")]
    [InlineData("UPDATE_PROFILE", "UPDATE")]
    [InlineData("DELETE_LOCATION", "DELETE")]
    [InlineData("LOCK_USER", "LOCK")]
    [InlineData("UNLOCK_USER", "UNLOCK")]
    [InlineData("APPROVE_COACH_APPLICATION", "APPROVE")]
    [InlineData("REJECT_COACH_APPLICATION", "REJECT")]
    public void DetermineActionType_ShouldReturnCanonicalActionType(string action, string expectedActionType)
    {
        var actual = SystemLogService.DetermineActionType(action);
        Assert.Equal(expectedActionType, actual);
    }
}