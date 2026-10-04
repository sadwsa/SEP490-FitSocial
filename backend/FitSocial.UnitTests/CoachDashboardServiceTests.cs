using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for UC_17: View Coach Dashboard (CoachDashboardService).
/// Tests calculation of the 4 key overview metrics under various scenarios.
/// </summary>
public class CoachDashboardServiceTests
{
    private readonly Mock<ICoachDashboardRepository> _dashboardRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly CoachDashboardService _service;

    public CoachDashboardServiceTests()
    {
        _dashboardRepoMock = new Mock<ICoachDashboardRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _service = new CoachDashboardService(_dashboardRepoMock.Object, _userRepoMock.Object);
    }

    [Fact]
    public async Task GetCoachDashboardMetricsAsync_WhenCoachHasData_ShouldReturnCorrectMetrics()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Coach Alexander",
            Email = "alexander.coach@fitsocial.com",
            RoleCode = "COACH"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _dashboardRepoMock.Setup(r => r.GetActiveTraineesCountAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);

        _dashboardRepoMock.Setup(r => r.GetTotalPackagesSoldAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(25);

        _dashboardRepoMock.Setup(r => r.GetMonthlyRevenueAsync(coachId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(15500000m);

        _dashboardRepoMock.Setup(r => r.GetPendingRequestsCountAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _service.GetCoachDashboardMetricsAsync(coachId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(8, result.Data.ActiveTraineesCount);
        Assert.Equal(25, result.Data.TotalPackagesSold);
        Assert.Equal(15500000m, result.Data.MonthlyRevenue);
        Assert.Equal(3, result.Data.PendingRequestsCount);
        Assert.Equal("VND", result.Data.Currency);
        Assert.True(result.Data.LastUpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetCoachDashboardMetricsAsync_WhenCoachHasNoData_ShouldReturnZeroMetrics()
    {
        // Arrange (new coach with no packages sold, no trainees, zero revenue, zero requests)
        var coachId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Newbie Coach",
            Email = "newbie.coach@fitsocial.com",
            RoleCode = "COACH"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _dashboardRepoMock.Setup(r => r.GetActiveTraineesCountAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _dashboardRepoMock.Setup(r => r.GetTotalPackagesSoldAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _dashboardRepoMock.Setup(r => r.GetMonthlyRevenueAsync(coachId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        _dashboardRepoMock.Setup(r => r.GetPendingRequestsCountAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetCoachDashboardMetricsAsync(coachId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.ActiveTraineesCount);
        Assert.Equal(0, result.Data.TotalPackagesSold);
        Assert.Equal(0m, result.Data.MonthlyRevenue);
        Assert.Equal(0, result.Data.PendingRequestsCount);
        Assert.Equal("VND", result.Data.Currency);
    }

    [Fact]
    public async Task GetCoachDashboardMetricsAsync_WhenCoachIdIsEmpty_ShouldThrowValidationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachDashboardMetricsAsync(Guid.Empty));

        Assert.Equal("Coach ID is required.", ex.Message);
    }

    [Fact]
    public async Task GetCoachDashboardMetricsAsync_WhenCoachNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentCoachId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(nonExistentCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachDashboardMetricsAsync(nonExistentCoachId));

        Assert.Equal("Coach account not found.", ex.Message);
    }
}
