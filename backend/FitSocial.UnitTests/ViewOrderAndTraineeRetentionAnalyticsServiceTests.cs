using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for View Order & Trainee Retention Analytics feature:
/// 1. CoachDashboardService.GetCoachDashboardAnalyticsAsync(Guid coachId, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID08, UTCID23): Full data, zero orders, 100% retention, 0% single-order retention, 6 consecutive months & VN startDateUtc, partial month zero-fill, 5 top trainees field mapping, empty top trainees, CancellationToken forwarding.
///    - Boundary (UTCID09 - UTCID16, UTCID24): 0.0% min retention, 100.0% max retention, fractional precision (33.3%), limit=5 verification, 10,000 orders volume, chronological MM/yyyy formatting, zero revenue with positive orders, 7-month past out-of-window exclusion, future out-of-window month exclusion.
///    - Abnormal (UTCID17 - UTCID22): Guid.Empty coachId, coach not found, summary DB exception, top trainees DB exception, monthly stats DB exception, cancellation token propagation.
/// 2. AnalyticsController.GetCoachAnalytics(Guid coachId, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID04, UTCID13): Self coach 200 OK, "sub" claim fallback 200 OK, ADMIN override 200 OK, STAFF override 200 OK, CancellationToken forwarding.
///    - Boundary (UTCID05, UTCID09): Empty string NameIdentifier claim 401 Unauthorized, whitespace NameIdentifier claim 401 Unauthorized.
///    - Abnormal (UTCID06 - UTCID08, UTCID10 - UTCID12): Missing claims 401, invalid GUID NameIdentifier 401, IDOR cross-coach access 403, invalid "sub" claim 401, TRAINEE role access 403, service NotFoundException propagation.
/// 3. CoachDashboardRepository.GetAnalyticsSummaryAsync (UTCID01 - UTCID08)
/// 4. CoachDashboardRepository.GetMonthlyStatsAsync (UTCID01 - UTCID05)
/// 5. CoachDashboardRepository.GetTopTraineesAsync (UTCID01 - UTCID05)
/// </summary>
public class ViewOrderAndTraineeRetentionAnalyticsServiceTests
{
    private readonly Mock<ICoachDashboardRepository> _mockDashboardRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly CoachDashboardService _service;

    public ViewOrderAndTraineeRetentionAnalyticsServiceTests()
    {
        _mockDashboardRepo = new Mock<ICoachDashboardRepository>();
        _mockUserRepo = new Mock<IUserRepository>();

        _service = new CoachDashboardService(
            _mockDashboardRepo.Object,
            _mockUserRepo.Object);
    }

    private User CreateCoachUser(Guid coachId)
    {
        return new User
        {
            UserId = coachId,
            FullName = "Coach David",
            Email = "david.coach@fitsocial.com",
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };
    }

    #region CoachDashboardService.GetCoachDashboardAnalyticsAsync - Normal (UTCID01 - UTCID08)

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenCoachHasFullData_ShouldReturnCorrectMetricsAnd6MonthsStats()
    {
        // UTCID01 - Normal: Active coach with orders, revenue, retention rate, monthly stats and top trainees
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);
        var trainee1 = Guid.NewGuid();
        var trainee2 = Guid.NewGuid();
        var trainee3 = Guid.NewGuid();

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((45000000m, 15, 60.0));

        var topTraineesRaw = new List<(Guid TraineeId, string TraineeName, decimal TotalSpent, int TotalOrders)>
        {
            (trainee1, "Trainee Alice", 15000000m, 5),
            (trainee2, "Trainee Bob", 12000000m, 4),
            (trainee3, "Trainee Charlie", 9000000m, 3)
        };
        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topTraineesRaw);

        var nowVn = DateTime.UtcNow.AddHours(7);
        var monthlyStatsRaw = new List<(int Year, int Month, decimal Revenue, int TotalOrders)>
        {
            (nowVn.Year, nowVn.Month, 20000000m, 6),
            (nowVn.AddMonths(-1).Year, nowVn.AddMonths(-1).Month, 15000000m, 5),
            (nowVn.AddMonths(-2).Year, nowVn.AddMonths(-2).Month, 10000000m, 4)
        };
        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthlyStatsRaw);

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Analytics retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(45000000m, result.Data.TotalRevenue);
        Assert.Equal(15, result.Data.TotalOrders);
        Assert.Equal(60.0, result.Data.RetentionRate);
        Assert.Equal(6, result.Data.MonthlyStats.Count);
        Assert.Equal(0m, result.Data.MonthlyStats[0].Revenue);
        Assert.Equal(0, result.Data.MonthlyStats[0].TotalOrders);
        Assert.Equal(0m, result.Data.MonthlyStats[1].Revenue);
        Assert.Equal(0m, result.Data.MonthlyStats[2].Revenue);
        Assert.Equal(10000000m, result.Data.MonthlyStats[3].Revenue);
        Assert.Equal(4, result.Data.MonthlyStats[3].TotalOrders);
        Assert.Equal(15000000m, result.Data.MonthlyStats[4].Revenue);
        Assert.Equal(5, result.Data.MonthlyStats[4].TotalOrders);
        Assert.Equal(20000000m, result.Data.MonthlyStats[5].Revenue);
        Assert.Equal(6, result.Data.MonthlyStats[5].TotalOrders);
        Assert.Equal(3, result.Data.TopTrainees.Count);
        Assert.Equal(trainee1, result.Data.TopTrainees[0].TraineeId);
        Assert.Equal("Trainee Alice", result.Data.TopTrainees[0].TraineeName);
        Assert.Equal(15000000m, result.Data.TopTrainees[0].TotalSpent);
        Assert.Equal(5, result.Data.TopTrainees[0].TotalOrders);
        Assert.Equal(trainee2, result.Data.TopTrainees[1].TraineeId);
        Assert.Equal("Trainee Bob", result.Data.TopTrainees[1].TraineeName);
        Assert.Equal(12000000m, result.Data.TopTrainees[1].TotalSpent);
        Assert.Equal(4, result.Data.TopTrainees[1].TotalOrders);
        Assert.Equal(trainee3, result.Data.TopTrainees[2].TraineeId);
        Assert.Equal("Trainee Charlie", result.Data.TopTrainees[2].TraineeName);
        Assert.Equal(9000000m, result.Data.TopTrainees[2].TotalSpent);
        Assert.Equal(3, result.Data.TopTrainees[2].TotalOrders);
        _mockUserRepo.Verify(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenCoachHasZeroOrders_ShouldReturnZeroOrdersAndZeroRetentionRate()
    {
        // UTCID02 - Normal: New coach with 0 orders, 0 revenue, 0.0% retention rate
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0m, 0, 0.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0m, result.Data.TotalRevenue);
        Assert.Equal(0, result.Data.TotalOrders);
        Assert.Equal(0.0, result.Data.RetentionRate);
        Assert.Empty(result.Data.TopTrainees);
        Assert.Equal(6, result.Data.MonthlyStats.Count);
        Assert.All(result.Data.MonthlyStats, m =>
        {
            Assert.Equal(0m, m.Revenue);
            Assert.Equal(0, m.TotalOrders);
        });
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenAllTraineesAreRetained_ShouldReturn100PercentRetentionRate()
    {
        // UTCID03 - Normal: All trainees purchased 2 or more packages -> 100% retention
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((30000000m, 10, 100.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(100.0, result.Data!.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTraineesHaveSingleOrdersOnly_ShouldReturnZeroPercentRetentionRate()
    {
        // UTCID04 - Normal: Multiple trainees but each made only 1 order -> 0% retention
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((15000000m, 5, 0.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0.0, result.Data!.RetentionRate);
        Assert.Equal(5, result.Data.TotalOrders);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_ShouldAlwaysReturnExactly6ConsecutiveMonthsInMonthlyStats()
    {
        // UTCID05 - Normal: Verify MonthlyStats returns 6 consecutive chronological months and passes VN-adjusted startDateUtc
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);
        DateTime capturedStartDate = default;

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10000000m, 4, 25.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, CancellationToken>((_, dt, _) => capturedStartDate = dt)
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(6, result.Data!.MonthlyStats.Count);

        var nowVn = DateTime.UtcNow.AddHours(7);
        var sixMonthsAgoVn = nowVn.AddMonths(-5);
        var startMonthVn = new DateTime(sixMonthsAgoVn.Year, sixMonthsAgoVn.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(startMonthVn.AddHours(-7), capturedStartDate);

        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(startMonthVn.AddMonths(i).ToString("MM/yyyy"), result.Data.MonthlyStats[i].Month);
        }
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenMonthlyStatsHasPartialMonths_ShouldFillMissingMonthsWithZero()
    {
        // UTCID06 - Normal: Data only exists for 1 month; remaining 5 months should have 0 revenue and 0 orders
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5000000m, 2, 50.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        var nowVn = DateTime.UtcNow.AddHours(7);
        var monthlyStatsRaw = new List<(int Year, int Month, decimal Revenue, int TotalOrders)>
        {
            (nowVn.Year, nowVn.Month, 5000000m, 2)
        };
        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthlyStatsRaw);

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(6, result.Data!.MonthlyStats.Count);
        Assert.Equal(5000000m, result.Data.MonthlyStats[5].Revenue);
        Assert.Equal(2, result.Data.MonthlyStats[5].TotalOrders);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(0m, result.Data.MonthlyStats[i].Revenue);
            Assert.Equal(0, result.Data.MonthlyStats[i].TotalOrders);
        }
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTopTraineesHas5Items_ShouldMapAll5TraineesCorrectly()
    {
        // UTCID07 - Normal: Exactly 5 top trainees mapped with all fields verified
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((50000000m, 20, 80.0));

        var traineesList = Enumerable.Range(1, 5).Select(i =>
            (Guid.NewGuid(), $"Trainee {i}", (decimal)(10000000 - i * 1000000), 5 - i + 1)).ToList();

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineesList);

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(5, result.Data!.TopTrainees.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(traineesList[i].Item1, result.Data.TopTrainees[i].TraineeId);
            Assert.Equal(traineesList[i].Item2, result.Data.TopTrainees[i].TraineeName);
            Assert.Equal(traineesList[i].Item3, result.Data.TopTrainees[i].TotalSpent);
            Assert.Equal(traineesList[i].Item4, result.Data.TopTrainees[i].TotalOrders);
        }
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTopTraineesIsEmpty_ShouldReturnEmptyTopTraineesList()
    {
        // UTCID08 - Normal: No trainees yet, returns empty list
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0m, 0, 0.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data!.TopTrainees);
        Assert.Empty(result.Data.TopTrainees);
    }

    #endregion

    #region CoachDashboardService.GetCoachDashboardAnalyticsAsync - Boundary (UTCID09 - UTCID16)

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenRetentionRateIsAtMinimumBoundary_0Percent_ShouldReturnZero()
    {
        // UTCID09 - Boundary: RetentionRate = 0.0% (minimum boundary)
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5000000m, 1, 0.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0.0, result.Data!.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenRetentionRateIsAtMaximumBoundary_100Percent_ShouldReturn100()
    {
        // UTCID10 - Boundary: RetentionRate = 100.0% (maximum boundary)
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((100000000m, 50, 100.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(100.0, result.Data!.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenRetentionRateHasDecimalPrecision_ShouldRetainPrecision()
    {
        // UTCID11 - Boundary: Fractional retention rate (e.g. 33.3%) preserves decimal precision
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((15000000m, 6, 33.3));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(33.3, result.Data!.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTopTraineesIsExactlyAtMaxLimit_5Items_ShouldLimitTo5()
    {
        // UTCID12 - Boundary: Verify repository query specifically requests top 5 trainees and maps all 5
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10000000m, 5, 50.0));

        var trainees = Enumerable.Range(1, 5)
            .Select(i => (Guid.NewGuid(), $"Top {i}", 2000000m * (6 - i), 1))
            .ToList();

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trainees);

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.TopTrainees.Count);
        _mockDashboardRepo.Verify(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTotalOrdersIsLargeBoundary_ShouldHandleCorrectly()
    {
        // UTCID13 - Boundary: High order volume boundary (10,000 orders and 50,000,000,000 VND revenue)
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((50000000000m, 10000, 75.5));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(10000, result.Data!.TotalOrders);
        Assert.Equal(50000000000m, result.Data.TotalRevenue);
        Assert.Equal(75.5, result.Data.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenMonthBoundaryRollsOverYear_ShouldFormatMonthYearCorrectly()
    {
        // UTCID14 - Boundary: Month formatting across all 6 consecutive MM/yyyy entries
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10000000m, 3, 33.3));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(6, result.Data!.MonthlyStats.Count);
        Assert.All(result.Data.MonthlyStats, m =>
        {
            Assert.Matches(@"^\d{2}/\d{4}$", m.Month);
        });
        for (int i = 1; i < result.Data.MonthlyStats.Count; i++)
        {
            var prev = DateTime.ParseExact(result.Data.MonthlyStats[i - 1].Month, "MM/yyyy", CultureInfo.InvariantCulture);
            var curr = DateTime.ParseExact(result.Data.MonthlyStats[i].Month, "MM/yyyy", CultureInfo.InvariantCulture);
            Assert.Equal(prev.AddMonths(1), curr);
        }
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenRevenueIsZeroWithPositiveOrders_ShouldHandleCorrectly()
    {
        // UTCID15 - Boundary: Free promotions or trials where TotalOrders > 0 but Revenue = 0
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0m, 5, 40.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0m, result.Data!.TotalRevenue);
        Assert.Equal(5, result.Data.TotalOrders);
        Assert.Equal(40.0, result.Data.RetentionRate);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenMonthlyStatsIncludesOutOfWindowMonth_ShouldExcludeFrom6MonthList()
    {
        // UTCID16 - Boundary: Data from 7 months ago (just outside 6-month window) is ignored in MonthlyStats
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((12000000m, 4, 50.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        var nowVn = DateTime.UtcNow.AddHours(7);
        var outOfWindow = nowVn.AddMonths(-6);
        var monthlyStatsRaw = new List<(int Year, int Month, decimal Revenue, int TotalOrders)>
        {
            (outOfWindow.Year, outOfWindow.Month, 99000000m, 99),
            (nowVn.Year, nowVn.Month, 12000000m, 4)
        };
        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(monthlyStatsRaw);

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(6, result.Data!.MonthlyStats.Count);
        Assert.DoesNotContain(result.Data.MonthlyStats, m => m.Revenue == 99000000m);
        Assert.Equal(12000000m, result.Data.MonthlyStats[5].Revenue);
    }

    #endregion

    #region CoachDashboardService.GetCoachDashboardAnalyticsAsync - Abnormal (UTCID17 - UTCID22)

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenCoachIdIsEmptyGuid_ShouldThrowValidationException()
    {
        // UTCID17 - Abnormal: Empty coachId throws ValidationException without querying repositories
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachDashboardAnalyticsAsync(Guid.Empty));

        Assert.Equal("Coach ID is required.", ex.Message);
        _mockUserRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDashboardRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenCoachNotFound_ShouldThrowNotFoundException()
    {
        // UTCID18 - Abnormal: Coach not found in database throws NotFoundException without querying analytics
        var nonExistentCoachId = Guid.NewGuid();

        _mockUserRepo.Setup(r => r.GetByIdAsync(nonExistentCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachDashboardAnalyticsAsync(nonExistentCoachId));

        Assert.Equal("Coach account not found.", ex.Message);
        _mockDashboardRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenSummaryQueryThrowsDatabaseException_ShouldPropagateException()
    {
        // UTCID19 - Abnormal: Database failure during GetAnalyticsSummaryAsync propagates exception
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database query timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetCoachDashboardAnalyticsAsync(coachId));

        Assert.Equal("Database query timeout", ex.Message);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenTopTraineesQueryThrowsException_ShouldPropagateException()
    {
        // UTCID20 - Abnormal: Database failure during GetTopTraineesAsync propagates exception
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10000000m, 2, 50.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _service.GetCoachDashboardAnalyticsAsync(coachId));

        Assert.Equal("Database connection failure", ex.Message);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenMonthlyStatsQueryThrowsException_ShouldPropagateException()
    {
        // UTCID21 - Abnormal: Database failure during GetMonthlyStatsAsync propagates exception
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((10000000m, 2, 50.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SQL syntax error in monthly stats"));

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _service.GetCoachDashboardAnalyticsAsync(coachId));

        Assert.Equal("SQL syntax error in monthly stats", ex.Message);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenCancellationRequested_ShouldPropagateOperationCanceledException()
    {
        // UTCID22 - Abnormal: Operation canceled via CancellationToken
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var coachId = Guid.NewGuid();

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetCoachDashboardAnalyticsAsync(coachId, cts.Token));
        _mockDashboardRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_ShouldForwardCancellationTokenToAllRepositories()
    {
        // UTCID23 - Normal: Explicit CancellationToken is forwarded to user and dashboard repositories
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, token))
            .ReturnsAsync(coach);
        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, token))
            .ReturnsAsync((1000000m, 1, 0.0));
        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, token))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());
        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), token))
            .ReturnsAsync(new List<(int, int, decimal, int)>());

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId, token);

        Assert.True(result.Success);
        _mockUserRepo.Verify(r => r.GetByIdAsync(coachId, token), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetAnalyticsSummaryAsync(coachId, token), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetTopTraineesAsync(coachId, 5, token), Times.Once);
        _mockDashboardRepo.Verify(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), token), Times.Once);
    }

    [Fact]
    public async Task GetCoachDashboardAnalyticsAsync_WhenMonthlyStatsIncludesFutureMonth_ShouldExcludeFrom6MonthList()
    {
        // UTCID24 - Boundary: Future month beyond current VN month is excluded from 6-month MonthlyStats window
        var coachId = Guid.NewGuid();
        var coach = CreateCoachUser(coachId);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coach);

        _mockDashboardRepo.Setup(r => r.GetAnalyticsSummaryAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((8000000m, 2, 50.0));

        _mockDashboardRepo.Setup(r => r.GetTopTraineesAsync(coachId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(Guid, string, decimal, int)>());

        var nowVn = DateTime.UtcNow.AddHours(7);
        var futureMonth = nowVn.AddMonths(1);
        _mockDashboardRepo.Setup(r => r.GetMonthlyStatsAsync(coachId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(int Year, int Month, decimal Revenue, int TotalOrders)>
            {
                (futureMonth.Year, futureMonth.Month, 77000000m, 15),
                (nowVn.Year, nowVn.Month, 8000000m, 2)
            });

        var result = await _service.GetCoachDashboardAnalyticsAsync(coachId);

        Assert.True(result.Success);
        Assert.Equal(6, result.Data!.MonthlyStats.Count);
        Assert.DoesNotContain(result.Data.MonthlyStats, m => m.Revenue == 77000000m);
        Assert.Equal(8000000m, result.Data.MonthlyStats[5].Revenue);
    }

    #endregion

    #region AnalyticsController.GetCoachAnalytics (UTCID01 - UTCID13)

    private static AnalyticsController CreateControllerWithClaims(Mock<ICoachDashboardService> mockService, params Claim[] claims)
    {
        var controller = new AnalyticsController(mockService.Object);
        var identity = claims.Length > 0
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenCoachAccessesOwnAnalytics_ShouldReturn200Ok()
    {
        // Controller UTCID01 - Normal: Coach accesses own analytics using NameIdentifier claim
        var mockService = new Mock<ICoachDashboardService>();
        var coachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var expectedDto = new CoachDashboardDto { TotalRevenue = 15000000m, TotalOrders = 6, RetentionRate = 50.0 };
        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDashboardDto>.Ok(expectedDto, "Analytics retrieved successfully."));

        var actionResult = await controller.GetCoachAnalytics(coachId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<ApiResponseDto<CoachDashboardDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Analytics retrieved successfully.", response.Message);
        Assert.Equal(15000000m, response.Data!.TotalRevenue);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(coachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenSubClaimUsed_ShouldReturn200Ok()
    {
        // Controller UTCID02 - Normal: Falls back to "sub" claim when NameIdentifier is absent
        var mockService = new Mock<ICoachDashboardService>();
        var coachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim("sub", coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var expectedDto = new CoachDashboardDto { TotalRevenue = 5000000m, TotalOrders = 2, RetentionRate = 100.0 };
        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDashboardDto>.Ok(expectedDto));

        var actionResult = await controller.GetCoachAnalytics(coachId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<ApiResponseDto<CoachDashboardDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(100.0, response.Data!.RetentionRate);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(coachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenAdminAccessesAnyCoach_ShouldReturn200Ok()
    {
        // Controller UTCID03 - Normal: Admin role can access any coach's analytics without IDOR restriction
        var mockService = new Mock<ICoachDashboardService>();
        var adminId = Guid.NewGuid();
        var targetCoachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Admin));

        var expectedDto = new CoachDashboardDto { TotalRevenue = 20000000m, TotalOrders = 8, RetentionRate = 50.0 };
        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(targetCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDashboardDto>.Ok(expectedDto));

        var actionResult = await controller.GetCoachAnalytics(targetCoachId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<ApiResponseDto<CoachDashboardDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(20000000m, response.Data!.TotalRevenue);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(targetCoachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenStaffAccessesAnyCoach_ShouldReturn200Ok()
    {
        // Controller UTCID04 - Normal: Staff role can access any coach's analytics without IDOR restriction
        var mockService = new Mock<ICoachDashboardService>();
        var staffId = Guid.NewGuid();
        var targetCoachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Staff));

        var expectedDto = new CoachDashboardDto { TotalRevenue = 8000000m, TotalOrders = 3, RetentionRate = 33.3 };
        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(targetCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDashboardDto>.Ok(expectedDto));

        var actionResult = await controller.GetCoachAnalytics(targetCoachId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var response = Assert.IsType<ApiResponseDto<CoachDashboardDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(8000000m, response.Data!.TotalRevenue);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(targetCoachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenNameIdentifierClaimIsEmptyString_ShouldReturn401Unauthorized()
    {
        // Controller UTCID05 - Boundary: Empty string NameIdentifier claim fails IsNullOrEmpty check -> 401 Unauthorized
        var mockService = new Mock<ICoachDashboardService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, string.Empty),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetCoachAnalytics(Guid.NewGuid(), CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(unauthorizedResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid user credentials.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Controller UTCID06 - Abnormal: Request has no claims -> 401 Unauthorized
        var mockService = new Mock<ICoachDashboardService>();
        var controller = CreateControllerWithClaims(mockService);

        var actionResult = await controller.GetCoachAnalytics(Guid.NewGuid(), CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(unauthorizedResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid user credentials.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenUserIdClaimIsInvalidGuidFormat_ShouldReturn401Unauthorized()
    {
        // Controller UTCID07 - Abnormal: Malformed non-GUID user ID claim -> 401 Unauthorized
        var mockService = new Mock<ICoachDashboardService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "not-a-valid-guid"),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetCoachAnalytics(Guid.NewGuid(), CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(unauthorizedResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid user credentials.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenAnotherCoachAccesses_ShouldReturn403Forbidden()
    {
        // Controller UTCID08 - Abnormal: IDOR violation (coach accessing another coach's data) -> 403 Forbidden
        var mockService = new Mock<ICoachDashboardService>();
        var callerCoachId = Guid.NewGuid();
        var targetCoachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, callerCoachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetCoachAnalytics(targetCoachId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(objectResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Access denied: You are only allowed to view your own analytics.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenNameIdentifierIsWhitespace_ShouldReturn401Unauthorized()
    {
        // Controller UTCID09 - Boundary: Whitespace NameIdentifier passes IsNullOrEmpty but fails Guid.TryParse -> 401 Unauthorized
        var mockService = new Mock<ICoachDashboardService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "   "),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetCoachAnalytics(Guid.NewGuid(), CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(unauthorizedResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized: Invalid user credentials.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenSubClaimIsInvalidGuid_ShouldReturn401Unauthorized()
    {
        // Controller UTCID10 - Abnormal: NameIdentifier absent and "sub" claim is invalid GUID -> 401 Unauthorized
        var mockService = new Mock<ICoachDashboardService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim("sub", "invalid-sub-guid"),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetCoachAnalytics(Guid.NewGuid(), CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenTraineeAccessesCoachAnalytics_ShouldReturn403Forbidden()
    {
        // Controller UTCID11 - Abnormal: Trainee role attempting to view a coach's analytics -> 403 Forbidden
        var mockService = new Mock<ICoachDashboardService>();
        var traineeId = Guid.NewGuid();
        var targetCoachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, traineeId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var actionResult = await controller.GetCoachAnalytics(targetCoachId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<object>>(objectResult.Value);
        Assert.False(body.Success);
        Assert.Equal("Access denied: You are only allowed to view your own analytics.", body.Message);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_WhenServiceThrowsNotFoundException_ShouldPropagateException()
    {
        // Controller UTCID12 - Abnormal: Admin queries non-existent coach -> NotFoundException propagates to exception middleware
        var mockService = new Mock<ICoachDashboardService>();
        var adminId = Guid.NewGuid();
        var missingCoachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Admin));

        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(missingCoachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Coach account not found."));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.GetCoachAnalytics(missingCoachId, CancellationToken.None));

        Assert.Equal("Coach account not found.", ex.Message);
    }

    [Fact]
    public async Task AnalyticsController_GetCoachAnalytics_ShouldForwardCancellationTokenToService()
    {
        // Controller UTCID13 - Normal: Explicit CancellationToken is passed through to ICoachDashboardService
        var mockService = new Mock<ICoachDashboardService>();
        var coachId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var expectedDto = new CoachDashboardDto { TotalRevenue = 1000000m, TotalOrders = 1, RetentionRate = 0.0 };
        mockService.Setup(s => s.GetCoachDashboardAnalyticsAsync(coachId, token))
            .ReturnsAsync(ApiResponseDto<CoachDashboardDto>.Ok(expectedDto));

        var actionResult = await controller.GetCoachAnalytics(coachId, token);

        Assert.IsType<OkObjectResult>(actionResult);
        mockService.Verify(s => s.GetCoachDashboardAnalyticsAsync(coachId, token), Times.Once);
    }

    #endregion

    #region CoachDashboardRepository InMemory Helpers

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    private static User CreateTestUser(Guid id, string? fullName = "Trainee User", string role = RoleConstants.Trainee)
    {
        return new User
        {
            UserId = id,
            FullName = fullName,
            Email = $"{id:N}@fitsocial.test",
            RoleCode = role
        };
    }

    #endregion

    #region CoachDashboardRepository.GetAnalyticsSummaryAsync (UTCID01 - UTCID08)

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_WithPaidOrdersAndRetainedTrainees_ShouldCalculateRevenueOrdersAndRetention()
    {
        // Repo Summary UTCID01 - Normal: Aggregates PAID/ACTIVE/COMPLETED orders and calculates retention (1 of 2 trainees >= 2 orders = 50.0%)
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var trainee1 = Guid.NewGuid();
        var trainee2 = Guid.NewGuid();
        context.Users.AddRange(
            CreateTestUser(trainee1, "Trainee 1"),
            CreateTestUser(trainee2, "Trainee 2"));

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = trainee1, TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = trainee1, TotalAmount = 2000000m, OrderStatus = PaymentConstants.OrderStatusCompleted, CreatedAt = DateTime.UtcNow },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = trainee2, TotalAmount = 1500000m, OrderStatus = PaymentConstants.OrderStatusActive, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(4500000m, totalRevenue);
        Assert.Equal(3, totalOrders);
        Assert.Equal(50.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_WhenMatchedViaOrderDetailsPackageCoachId_ShouldIncludeOrder()
    {
        // Repo Summary UTCID02 - Normal: Order with CoachId = null matched through OrderDetails -> Package.CoachId
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        context.Users.Add(CreateTestUser(traineeId, "Trainee Package"));
        context.TrainingPackages.Add(new TrainingPackage { PackageId = packageId, CoachId = coachId, Title = "Strength 101" });

        var orderId = Guid.NewGuid();
        context.Orders.Add(new Order
        {
            OrderId = orderId,
            CoachId = null,
            BuyerId = traineeId,
            TotalAmount = 3200000m,
            OrderStatus = PaymentConstants.OrderStatusPaid,
            CreatedAt = DateTime.UtcNow
        });
        context.OrderDetails.Add(new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = orderId,
            PackageId = packageId
        });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(3200000m, totalRevenue);
        Assert.Equal(1, totalOrders);
        Assert.Equal(0.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_ShouldExcludeNonPaidStatusesAndOtherCoaches_AndAllowLowercasePaid()
    {
        // Repo Summary UTCID03 - Normal: Excludes PENDING/CANCELLED/FAILED/REFUNDED and other coaches; case-insensitive "paid" included
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId, "Trainee Filter"));

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 1000000m, OrderStatus = "paid" },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 9999999m, OrderStatus = PaymentConstants.OrderStatusPending },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 9999999m, OrderStatus = PaymentConstants.OrderStatusCancelled },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 9999999m, OrderStatus = PaymentConstants.OrderStatusFailed },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 9999999m, OrderStatus = PaymentConstants.OrderStatusRefunded },
            new Order { OrderId = Guid.NewGuid(), CoachId = otherCoachId, BuyerId = traineeId, TotalAmount = 8888888m, OrderStatus = PaymentConstants.OrderStatusPaid });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(1000000m, totalRevenue);
        Assert.Equal(1, totalOrders);
        Assert.Equal(0.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_ShouldExcludeApprovedRefundsButKeepPendingOrRejectedRefunds()
    {
        // Repo Summary UTCID04 - Normal: Excludes orders with APPROVED refund request; keeps PENDING and REJECTED refund orders
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId, "Trainee Refund"));

        var approvedOrderId = Guid.NewGuid();
        var pendingOrderId = Guid.NewGuid();
        var rejectedOrderId = Guid.NewGuid();

        context.Orders.AddRange(
            new Order { OrderId = approvedOrderId, CoachId = coachId, BuyerId = traineeId, TotalAmount = 5000000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = pendingOrderId, CoachId = coachId, BuyerId = traineeId, TotalAmount = 1200000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = rejectedOrderId, CoachId = coachId, BuyerId = traineeId, TotalAmount = 1800000m, OrderStatus = PaymentConstants.OrderStatusCompleted });

        context.RefundRequests.AddRange(
            new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = approvedOrderId, RequestedBy = traineeId, Status = "APPROVED" },
            new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = pendingOrderId, RequestedBy = traineeId, Status = "PENDING" },
            new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = rejectedOrderId, RequestedBy = traineeId, Status = "REJECTED" });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(3000000m, totalRevenue);
        Assert.Equal(2, totalOrders);
        Assert.Equal(100.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_WhenNoOrdersExist_ShouldReturnZeros()
    {
        // Repo Summary UTCID05 - Boundary: Empty order set returns (0m, 0, 0.0) without division by zero
        using var context = CreateInMemoryDbContext();
        var repo = new CoachDashboardRepository(context);

        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(Guid.NewGuid());

        Assert.Equal(0m, totalRevenue);
        Assert.Equal(0, totalOrders);
        Assert.Equal(0.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_RetentionBoundary_1Of3TraineesWith2Orders_ShouldRoundTo33Point3()
    {
        // Repo Summary UTCID06 - Boundary: 1 trainee with exactly 2 orders (retained) and 2 trainees with 1 order -> 1/3 * 100 rounded to 1 decimal = 33.3
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        context.Users.AddRange(CreateTestUser(t1), CreateTestUser(t2), CreateTestUser(t3));

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t1, TotalAmount = 500000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t1, TotalAmount = 500000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t2, TotalAmount = 500000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t3, TotalAmount = 500000m, OrderStatus = PaymentConstants.OrderStatusPaid });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(2000000m, totalRevenue);
        Assert.Equal(4, totalOrders);
        Assert.Equal(33.3, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_WhenTotalAmountOrOrderStatusIsNull_ShouldHandleSafely()
    {
        // Repo Summary UTCID07 - Boundary: Null TotalAmount defaults to 0m; null OrderStatus is excluded
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId));

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = null, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 500000m, OrderStatus = null });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var (totalRevenue, totalOrders, retentionRate) = await repo.GetAnalyticsSummaryAsync(coachId);

        Assert.Equal(0m, totalRevenue);
        Assert.Equal(1, totalOrders);
        Assert.Equal(0.0, retentionRate);
    }

    [Fact]
    public async Task Repository_GetAnalyticsSummaryAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo Summary UTCID08 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new CoachDashboardRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetAnalyticsSummaryAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion

    #region CoachDashboardRepository.GetMonthlyStatsAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task Repository_GetMonthlyStatsAsync_ShouldGroupPaidOrdersByVietnamMonthAndSumRevenue()
    {
        // Repo Monthly UTCID01 - Normal: Groups valid orders by UTC+7 year/month and sums revenue & counts
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId));

        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = new DateTime(2026, 2, 10, 5, 0, 0, DateTimeKind.Utc) },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 2500000m, OrderStatus = PaymentConstants.OrderStatusCompleted, CreatedAt = new DateTime(2026, 2, 20, 5, 0, 0, DateTimeKind.Utc) },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 1500000m, OrderStatus = PaymentConstants.OrderStatusActive, CreatedAt = new DateTime(2026, 3, 5, 5, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var stats = await repo.GetMonthlyStatsAsync(coachId, startDate);

        Assert.Equal(2, stats.Count);
        var feb = stats.Single(s => s.Year == 2026 && s.Month == 2);
        var mar = stats.Single(s => s.Year == 2026 && s.Month == 3);
        Assert.Equal(3500000m, feb.Revenue);
        Assert.Equal(2, feb.TotalOrders);
        Assert.Equal(1500000m, mar.Revenue);
        Assert.Equal(1, mar.TotalOrders);
    }

    [Fact]
    public async Task Repository_GetMonthlyStatsAsync_ShouldExcludePendingRefundedApprovedRefundAndOtherCoachOrders()
    {
        // Repo Monthly UTCID02 - Normal: Excludes non-paid statuses, approved refunds, and other coaches
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId));

        var approvedOrderId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var orderTime = new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc);

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 2000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = orderTime },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 5000000m, OrderStatus = PaymentConstants.OrderStatusPending, CreatedAt = orderTime },
            new Order { OrderId = approvedOrderId, CoachId = coachId, BuyerId = traineeId, TotalAmount = 7000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = orderTime },
            new Order { OrderId = Guid.NewGuid(), CoachId = otherCoachId, BuyerId = traineeId, TotalAmount = 9000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = orderTime });
        context.RefundRequests.Add(new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = approvedOrderId, RequestedBy = traineeId, Status = "APPROVED" });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var stats = await repo.GetMonthlyStatsAsync(coachId, startDate);

        var apr = Assert.Single(stats);
        Assert.Equal(2026, apr.Year);
        Assert.Equal(4, apr.Month);
        Assert.Equal(2000000m, apr.Revenue);
        Assert.Equal(1, apr.TotalOrders);
    }

    [Fact]
    public async Task Repository_GetMonthlyStatsAsync_VietnamTimezoneMidnightAndStartDateBoundaries_ShouldMapAccurately()
    {
        // Repo Monthly UTCID03 - Boundary:
        // - 2026-04-30 18:00:00 UTC == 2026-05-01 01:00:00 VN (UTC+7) -> May 2026
        // - CreatedAt == startDate is included; CreatedAt < startDate is excluded
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId));

        var startDate = new DateTime(2026, 4, 30, 17, 0, 0, DateTimeKind.Utc); // 2026-05-01 00:00:00 VN
        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = startDate },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 2000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = new DateTime(2026, 4, 30, 18, 0, 0, DateTimeKind.Utc) },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 9999999m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = startDate.AddSeconds(-1) });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var stats = await repo.GetMonthlyStatsAsync(coachId, startDate);

        var may = Assert.Single(stats);
        Assert.Equal(2026, may.Year);
        Assert.Equal(5, may.Month);
        Assert.Equal(3000000m, may.Revenue);
        Assert.Equal(2, may.TotalOrders);
    }

    [Fact]
    public async Task Repository_GetMonthlyStatsAsync_WhenCreatedAtIsNullOrTotalAmountIsNull_ShouldExcludeNullDateAndDefaultNullAmount()
    {
        // Repo Monthly UTCID04 - Boundary: CreatedAt == null excluded; TotalAmount == null defaults to 0m
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId));

        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = null, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 5000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = null });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var stats = await repo.GetMonthlyStatsAsync(coachId, startDate);

        var feb = Assert.Single(stats);
        Assert.Equal(0m, feb.Revenue);
        Assert.Equal(1, feb.TotalOrders);
    }

    [Fact]
    public async Task Repository_GetMonthlyStatsAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo Monthly UTCID05 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new CoachDashboardRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetMonthlyStatsAsync(Guid.NewGuid(), DateTime.UtcNow.AddMonths(-6), cts.Token));
    }

    #endregion

    #region CoachDashboardRepository.GetTopTraineesAsync (UTCID01 - UTCID05)

    [Fact]
    public async Task Repository_GetTopTraineesAsync_ShouldAggregateAndOrderByTotalSpentDescending()
    {
        // Repo TopTrainees UTCID01 - Normal: Aggregates per trainee and orders descending by TotalSpent
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        context.Users.AddRange(
            CreateTestUser(t1, "Trainee Low"),
            CreateTestUser(t2, "Trainee High"));

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t1, TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t2, TotalAmount = 2000000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = t2, TotalAmount = 3000000m, OrderStatus = PaymentConstants.OrderStatusCompleted });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var top = await repo.GetTopTraineesAsync(coachId, 5);

        Assert.Equal(2, top.Count);
        Assert.Equal(t2, top[0].TraineeId);
        Assert.Equal("Trainee High", top[0].TraineeName);
        Assert.Equal(5000000m, top[0].TotalSpent);
        Assert.Equal(2, top[0].TotalOrders);
        Assert.Equal(t1, top[1].TraineeId);
        Assert.Equal("Trainee Low", top[1].TraineeName);
        Assert.Equal(1000000m, top[1].TotalSpent);
        Assert.Equal(1, top[1].TotalOrders);
    }

    [Fact]
    public async Task Repository_GetTopTraineesAsync_ShouldIncludePackageMatchAndExcludeRefundedOrPendingOrders()
    {
        // Repo TopTrainees UTCID02 - Normal: Includes orders matched via Package.CoachId; excludes PENDING and APPROVED refund orders
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId, "Trainee Mixed"));
        context.TrainingPackages.Add(new TrainingPackage { PackageId = packageId, CoachId = coachId, Title = "Pkg" });

        var pkgOrderId = Guid.NewGuid();
        var refundedOrderId = Guid.NewGuid();
        context.Orders.AddRange(
            new Order { OrderId = pkgOrderId, CoachId = null, BuyerId = traineeId, TotalAmount = 2500000m, OrderStatus = PaymentConstants.OrderStatusActive },
            new Order { OrderId = refundedOrderId, CoachId = coachId, BuyerId = traineeId, TotalAmount = 9000000m, OrderStatus = PaymentConstants.OrderStatusPaid },
            new Order { OrderId = Guid.NewGuid(), CoachId = coachId, BuyerId = traineeId, TotalAmount = 8000000m, OrderStatus = PaymentConstants.OrderStatusPending });
        context.OrderDetails.Add(new OrderDetail { OrderDetailsId = Guid.NewGuid(), OrderId = pkgOrderId, PackageId = packageId });
        context.RefundRequests.Add(new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = refundedOrderId, RequestedBy = traineeId, Status = "APPROVED" });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var top = await repo.GetTopTraineesAsync(coachId, 5);

        var single = Assert.Single(top);
        Assert.Equal(traineeId, single.TraineeId);
        Assert.Equal(2500000m, single.TotalSpent);
        Assert.Equal(1, single.TotalOrders);
    }

    [Fact]
    public async Task Repository_GetTopTraineesAsync_WhenMoreThanLimitExist_ShouldReturnStrictlyTopLimit()
    {
        // Repo TopTrainees UTCID03 - Boundary: 6 trainees present, limit=5 returns top 5 highest spenders
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        for (int i = 1; i <= 6; i++)
        {
            var tid = Guid.NewGuid();
            context.Users.Add(CreateTestUser(tid, $"Trainee {i}"));
            context.Orders.Add(new Order
            {
                OrderId = Guid.NewGuid(),
                CoachId = coachId,
                BuyerId = tid,
                TotalAmount = i * 1000000m,
                OrderStatus = PaymentConstants.OrderStatusPaid
            });
        }
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var top = await repo.GetTopTraineesAsync(coachId, 5);

        Assert.Equal(5, top.Count);
        Assert.Equal("Trainee 6", top[0].TraineeName);
        Assert.Equal(6000000m, top[0].TotalSpent);
        Assert.Equal("Trainee 2", top[4].TraineeName);
        Assert.Equal(2000000m, top[4].TotalSpent);
        Assert.DoesNotContain(top, t => t.TraineeName == "Trainee 1");
    }

    [Fact]
    public async Task Repository_GetTopTraineesAsync_WhenLimitIsZeroOrFullNameIsNull_ShouldHandleBoundaries()
    {
        // Repo TopTrainees UTCID04 - Boundary: Null FullName falls back to "Unknown User"; limit = 0 returns empty list
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        context.Users.Add(CreateTestUser(traineeId, null));
        context.Orders.Add(new Order
        {
            OrderId = Guid.NewGuid(),
            CoachId = coachId,
            BuyerId = traineeId,
            TotalAmount = null,
            OrderStatus = PaymentConstants.OrderStatusPaid
        });
        await context.SaveChangesAsync();

        var repo = new CoachDashboardRepository(context);
        var withNameFallback = await repo.GetTopTraineesAsync(coachId, 5);
        var withZeroLimit = await repo.GetTopTraineesAsync(coachId, 0);

        var item = Assert.Single(withNameFallback);
        Assert.Equal("Unknown User", item.TraineeName);
        Assert.Equal(0m, item.TotalSpent);
        Assert.Empty(withZeroLimit);
    }

    [Fact]
    public async Task Repository_GetTopTraineesAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo TopTrainees UTCID05 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new CoachDashboardRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetTopTraineesAsync(Guid.NewGuid(), 5, cts.Token));
    }

    #endregion
}
