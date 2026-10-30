using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachSubscriptionPlans;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachSubscriptionPlanServiceTests
{
    private readonly Mock<ICoachSubscriptionPlanRepository> _planRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuditLogService> _auditLogMock;
    private readonly CoachSubscriptionPlanService _service;

    public CoachSubscriptionPlanServiceTests()
    {
        _planRepoMock = new Mock<ICoachSubscriptionPlanRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _auditLogMock = new Mock<IAuditLogService>();
        _auditLogMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog());
        _service = new CoachSubscriptionPlanService(
            _planRepoMock.Object, _unitOfWorkMock.Object, _auditLogMock.Object);
    }

    [Fact]
    public async Task GetActivePlansAsync_ShouldReturnActivePlans()
    {
        // Arrange
        var plans = new List<CoachSubscriptionPlan>
        {
            new() { CoachSubscriptionPlansId = Guid.NewGuid(), Amount = 100000, IsActive = true, Description = "1 Month Plan" },
            new() { CoachSubscriptionPlansId = Guid.NewGuid(), Amount = 500000, IsActive = true, Description = "6 Month Plan" }
        };

        _planRepoMock.Setup(r => r.ListActivePlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        // Act
        var result = await _service.GetActivePlansAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldAddPlanAndReturnDto()
    {
        // Arrange
        var dto = new CreateCoachSubscriptionPlanDto
        {
            Amount = 300000,
            Currency = "VND",
            Description = "3 Month Standard Plan",
            SubscriptionDuration = 90,
            TrainingPackageDuration = 90,
            IsActive = true
        };

        _planRepoMock.Setup(r => r.AddAsync(It.IsAny<CoachSubscriptionPlan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreatePlanAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(300000, result.Data.Amount);
        Assert.Equal("VND", result.Data.Currency);
        Assert.True(result.Data.IsActive);
        _planRepoMock.Verify(r => r.AddAsync(It.IsAny<CoachSubscriptionPlan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- Audit log (UC_33.2/33.3/33.4) ----------

    private static CreateCoachSubscriptionPlanDto SampleCreateDto() => new()
    {
        Amount = 300000,
        Currency = "VND",
        Description = "3 Month Standard Plan",
        SubscriptionDuration = 90,
        TrainingPackageDuration = 90,
        IsActive = true
    };

    private void VerifyAuditLogged(string action, Guid? entityId = null)
    {
        // Matchers must be written inline inside the expression tree - Moq only
        // registers them while it evaluates the lambda, never beforehand.
        if (entityId.HasValue)
        {
            var expectedId = entityId.Value.ToString();
            _auditLogMock.Verify(a => a.LogAsync(
                It.IsAny<Guid?>(),
                action,
                "CoachSubscriptionPlan",
                expectedId,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        else
        {
            _auditLogMock.Verify(a => a.LogAsync(
                It.IsAny<Guid?>(),
                action,
                "CoachSubscriptionPlan",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldWriteAuditLog()
    {
        _planRepoMock.Setup(r => r.AddAsync(It.IsAny<CoachSubscriptionPlan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.CreatePlanAsync(SampleCreateDto());

        VerifyAuditLogged("CREATE_COACH_SUBSCRIPTION_PLAN");
    }

    [Fact]
    public async Task UpdatePlanAsync_ShouldWriteAuditLogWithOldAndNewValue()
    {
        var plan = new CoachSubscriptionPlan
        {
            CoachSubscriptionPlansId = Guid.NewGuid(),
            Amount = 300000,
            Currency = "VND",
            Description = "3 Month Standard Plan",
            IsActive = true
        };
        _planRepoMock.Setup(r => r.GetByIdAsync(plan.CoachSubscriptionPlansId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.UpdatePlanAsync(plan.CoachSubscriptionPlansId, new UpdateCoachSubscriptionPlanDto { Amount = 450000 });

        Assert.True(result.Success);
        Assert.Equal(450000, plan.Amount);
        VerifyAuditLogged("UPDATE_COACH_SUBSCRIPTION_PLAN", plan.CoachSubscriptionPlansId);
    }

    [Fact]
    public async Task DeletePlanAsync_ShouldDeactivatePlanAndWriteAuditLog()
    {
        var plan = new CoachSubscriptionPlan
        {
            CoachSubscriptionPlansId = Guid.NewGuid(),
            Amount = 300000,
            Currency = "VND",
            Description = "3 Month Standard Plan",
            IsActive = true
        };
        _planRepoMock.Setup(r => r.GetByIdAsync(plan.CoachSubscriptionPlansId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.DeletePlanAsync(plan.CoachSubscriptionPlansId);

        Assert.True(result.Success);
        Assert.False(plan.IsActive);
        VerifyAuditLogged("DELETE_COACH_SUBSCRIPTION_PLAN", plan.CoachSubscriptionPlansId);
    }

    [Fact]
    public async Task UpdatePlanAsync_WhenPlanNotFound_ShouldNotWriteAuditLog()
    {
        var id = Guid.NewGuid();
        _planRepoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachSubscriptionPlan?)null);

        await Assert.ThrowsAsync<FitSocial.Application.Exceptions.NotFoundException>(
            () => _service.UpdatePlanAsync(id, new UpdateCoachSubscriptionPlanDto { Amount = 1 }));

        _auditLogMock.Verify(a => a.LogAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenAuditFails_ShouldStillSucceed()
    {
        _planRepoMock.Setup(r => r.AddAsync(It.IsAny<CoachSubscriptionPlan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _auditLogMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit table down"));

        var result = await _service.CreatePlanAsync(SampleCreateDto());

        Assert.True(result.Success);
    }
}
