using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.Analytics;

public class RevenueAnalyticsFilterModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string Granularity { get; set; } = "DAILY"; // DAILY, WEEKLY, MONTHLY
    public string QuickFilter { get; set; } = "THIS_MONTH"; // TODAY, 7D, THIS_MONTH, LAST_MONTH, THIS_YEAR
}

public class RevenueOverviewModel
{
    public decimal TotalGMV { get; set; }
    public decimal TraineePackagesGMV { get; set; }
    public decimal PlatformDirectRevenue { get; set; }
    public decimal TotalApprovedRefunds { get; set; }
    public decimal TotalCoachPayouts { get; set; }
    public decimal PlatformNetCashHolding { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int TotalOrdersCount { get; set; }
    public decimal PreviousPeriodTotalGMV { get; set; }
    public decimal PreviousPeriodPlatformRevenue { get; set; }
    public double MoMGrowthRatePercentage { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class RevenueTimeSeriesPointModel
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalGMV { get; set; }
    public decimal TraineePackageGMV { get; set; }
    public decimal CoachSubscriptionRevenue { get; set; }
    public decimal ApprovedRefunds { get; set; }
}

public class RevenueBreakdownModel
{
    public decimal TraineePackageSales { get; set; }
    public decimal CoachSubscriptionSales { get; set; }
    public double TraineePackagePercentage { get; set; }
    public double CoachSubscriptionPercentage { get; set; }
}

public class TopCoachRevenueModel
{
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string CoachEmail { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public decimal GrossSales { get; set; }
    public int CompletedOrdersCount { get; set; }
    public decimal TotalPayouts { get; set; }
    public decimal RefundAmount { get; set; }
}

public class TopTrainingPackageModel
{
    public Guid PackageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class CoachSubscriptionPlanStatModel
{
    public Guid PlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int SubscribersCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class FullRevenueAnalyticsResponseModel
{
    public RevenueOverviewModel Overview { get; set; } = new();
    public List<RevenueTimeSeriesPointModel> TimeSeries { get; set; } = new();
    public RevenueBreakdownModel Breakdown { get; set; } = new();
    public List<TopCoachRevenueModel> TopCoaches { get; set; } = new();
    public List<TopTrainingPackageModel> TopPackages { get; set; } = new();
    public List<CoachSubscriptionPlanStatModel> SubscriptionPlanStats { get; set; } = new();
}
