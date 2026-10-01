using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachSubscriptionPlans;
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
    private readonly CoachSubscriptionPlanService _service;

    public CoachSubscriptionPlanServiceTests()
    {
        _planRepoMock = new Mock<ICoachSubscriptionPlanRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new CoachSubscriptionPlanService(_planRepoMock.Object, _unitOfWorkMock.Object);
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
}
