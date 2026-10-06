using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Analytics;
using FitSocial.Application.Services;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Models.Analytics;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for UC_27: Admin View System Revenue Analytics (RevenueAnalyticsService).
/// Verifies the 0% commission business model (Trainee package GMV belongs 100% to Coach;
/// Platform Direct Revenue = 100% Coach Subscription Plans).
/// </summary>
public class RevenueAnalyticsServiceTests
{
    private readonly Mock<IRevenueAnalyticsRepository> _repoMock;
    private readonly RevenueAnalyticsService _service;

    public RevenueAnalyticsServiceTests()
    {
        _repoMock = new Mock<IRevenueAnalyticsRepository>();
        _service = new RevenueAnalyticsService(_repoMock.Object);
    }

    [Fact]
    public async Task GetRevenueAnalyticsAsync_ReflectsZeroCommissionModel_PlatformDirectRevenueEqualsCoachSubscriptions()
    {
        // Arrange
        // Scenario:
        // Trainee packages GMV = 100,000,000 VND (100% belongs to Coaches)
        // Coach subscription plans = 20,000,000 VND (100% belongs to FitSocial Platform)
        // Coach subscription refund = 2,000,000 VND
        // Total approved refunds = 5,000,000 VND (including trainee + coach)
        // Processed coach payouts = 80,000,000 VND
        // Completed orders = 50
        var currentOverview = new RevenueOverviewRawModel
        {
            TotalGMV = 120_000_000m,
            TraineePackagesGMV = 100_000_000m,
            CoachSubscriptionsGMV = 20_000_000m,
            CoachSubscriptionRefunds = 2_000_000m,
            TotalApprovedRefunds = 5_000_000m,
            TotalCoachPayouts = 80_000_000m,
            CompletedOrdersCount = 50,
            TotalOrdersCount = 60
        };

        var prevOverview = new RevenueOverviewRawModel
        {
            TotalGMV = 100_000_000m,
            TraineePackagesGMV = 85_000_000m,
            CoachSubscriptionsGMV = 15_000_000m,
            CoachSubscriptionRefunds = 0m,
            TotalApprovedRefunds = 3_000_000m,
            TotalCoachPayouts = 70_000_000m,
            CompletedOrdersCount = 40,
            TotalOrdersCount = 45
        };

        _repoMock.Setup(r => r.GetRevenueOverviewAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentOverview);

        // Previous period mock
        _repoMock.Setup(r => r.GetRevenueOverviewAsync(It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-25)), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prevOverview);

        _repoMock.Setup(r => r.GetRevenueTimeSeriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RevenueTimeSeriesPointModel>());

        _repoMock.Setup(r => r.GetRevenueBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueBreakdownModel
            {
                TraineePackageSales = 100_000_000m,
                CoachSubscriptionSales = 20_000_000m,
                TraineePackagePercentage = 83.3,
                CoachSubscriptionPercentage = 16.7
            });

        _repoMock.Setup(r => r.GetTopCoachesByGrossSalesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopCoachRevenueModel>());

        _repoMock.Setup(r => r.GetTopSellingPackagesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopTrainingPackageModel>());

        _repoMock.Setup(r => r.GetCoachSubscriptionPlansStatsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachSubscriptionPlanStatModel>());

        var filter = new RevenueAnalyticsFilterDto
        {
            QuickFilter = "THIS_MONTH"
        };

        // Act
        var response = await _service.GetRevenueAnalyticsAsync(filter);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        var data = response.Data;
        Assert.NotNull(data);

        // 1. Total GMV is the sum of both streams
        Assert.Equal(120_000_000m, data.Overview.TotalGMV);

        // 2. Trainee packages GMV belongs to Coach (Platform take rate is 0%)
        Assert.Equal(100_000_000m, data.Overview.TraineePackagesGMV);

        // 3. Platform Direct Revenue = Coach Subscriptions (20M) - Coach Refunds (2M) = 18M VND
        Assert.Equal(18_000_000m, data.Overview.PlatformDirectRevenue);

        // 4. Platform Net Cash Holding = TotalGMV (120M) - TotalCoachPayouts (80M) - TotalApprovedRefunds (5M) = 35M VND
        Assert.Equal(35_000_000m, data.Overview.PlatformNetCashHolding);

        // 5. AOV = 120M / 50 orders = 2.4M VND
        Assert.Equal(2_400_000m, data.Overview.AverageOrderValue);
        Assert.Equal(50, data.Overview.CompletedOrdersCount);
        Assert.Equal(60, data.Overview.TotalOrdersCount);
    }

    [Fact]
    public async Task GetRevenueAnalyticsAsync_CalculatesMoMGrowthRate_Correctly()
    {
        // Arrange
        var currentOverview = new RevenueOverviewRawModel
        {
            TotalGMV = 150_000_000m,
            TraineePackagesGMV = 120_000_000m,
            CoachSubscriptionsGMV = 30_000_000m,
            CompletedOrdersCount = 60,
            TotalOrdersCount = 65
        };

        var prevOverview = new RevenueOverviewRawModel
        {
            TotalGMV = 100_000_000m,
            TraineePackagesGMV = 80_000_000m,
            CoachSubscriptionsGMV = 20_000_000m,
            CompletedOrdersCount = 40,
            TotalOrdersCount = 45
        };

        // Current period overview setup
        _repoMock.SetupSequence(r => r.GetRevenueOverviewAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentOverview)
            .ReturnsAsync(prevOverview);

        _repoMock.Setup(r => r.GetRevenueTimeSeriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RevenueTimeSeriesPointModel>());
        _repoMock.Setup(r => r.GetRevenueBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueBreakdownModel());
        _repoMock.Setup(r => r.GetTopCoachesByGrossSalesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopCoachRevenueModel>());
        _repoMock.Setup(r => r.GetTopSellingPackagesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopTrainingPackageModel>());
        _repoMock.Setup(r => r.GetCoachSubscriptionPlansStatsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachSubscriptionPlanStatModel>());

        var filter = new RevenueAnalyticsFilterDto { QuickFilter = "THIS_MONTH" };

        // Act
        var result = await _service.GetRevenueAnalyticsAsync(filter);

        // Assert
        // Growth = (150M - 100M) / 100M * 100% = +50.0%
        Assert.Equal(50.0, result.Data!.Overview.MoMGrowthRatePercentage);
    }

    [Fact]
    public async Task ExportRevenueCsvAsync_GeneratesUtf8BomAndCompleteSections()
    {
        // Arrange
        var currentOverview = new RevenueOverviewRawModel
        {
            TotalGMV = 50_000_000m,
            TraineePackagesGMV = 40_000_000m,
            CoachSubscriptionsGMV = 10_000_000m,
            CoachSubscriptionRefunds = 0m,
            TotalApprovedRefunds = 1_000_000m,
            TotalCoachPayouts = 30_000_000m,
            CompletedOrdersCount = 20,
            TotalOrdersCount = 22
        };

        _repoMock.Setup(r => r.GetRevenueOverviewAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentOverview);
        _repoMock.Setup(r => r.GetRevenueTimeSeriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RevenueTimeSeriesPointModel>
            {
                new() { PeriodKey = "01/10", Date = DateTime.UtcNow, TotalGMV = 50_000_000m, TraineePackageGMV = 40_000_000m, CoachSubscriptionRevenue = 10_000_000m }
            });
        _repoMock.Setup(r => r.GetRevenueBreakdownAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueBreakdownModel());
        _repoMock.Setup(r => r.GetTopCoachesByGrossSalesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopCoachRevenueModel>
            {
                new() { CoachId = Guid.NewGuid(), CoachName = "Coach John", CoachEmail = "john@fitsocial.com", GrossSales = 40_000_000m, CompletedOrdersCount = 15 }
            });
        _repoMock.Setup(r => r.GetTopSellingPackagesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopTrainingPackageModel>());
        _repoMock.Setup(r => r.GetCoachSubscriptionPlansStatsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachSubscriptionPlanStatModel>());

        var filter = new RevenueAnalyticsFilterDto { QuickFilter = "THIS_MONTH" };

        // Act
        var csvBytes = await _service.ExportRevenueCsvAsync(filter);

        // Assert
        Assert.NotEmpty(csvBytes);
        // Verify UTF-8 BOM: 0xEF, 0xBB, 0xBF
        Assert.Equal(0xEF, csvBytes[0]);
        Assert.Equal(0xBB, csvBytes[1]);
        Assert.Equal(0xBF, csvBytes[2]);

        var csvText = Encoding.UTF8.GetString(csvBytes);
        Assert.Contains("BÁO CÁO DOANH THU HỆ THỐNG FITSOCIAL", csvText);
        Assert.Contains("Coach John", csvText);
        Assert.Contains("50.000.000", csvText);
    }
}
