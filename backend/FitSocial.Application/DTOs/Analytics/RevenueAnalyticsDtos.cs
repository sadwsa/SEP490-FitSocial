using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.Analytics;

public class RevenueAnalyticsFilterDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Granularity { get; set; } // "DAILY", "WEEKLY", "MONTHLY"
    public string? QuickFilter { get; set; } // "TODAY", "7D", "THIS_MONTH", "LAST_MONTH", "THIS_YEAR"
}

public class RevenueOverviewDto
{
    /// <summary>
    /// Total Gross Merchandise Value (GMV) across all successful platform transactions.
    /// GMV = TraineePackagesGMV + CoachSubscriptionsGMV.
    /// </summary>
    public decimal TotalGMV { get; set; }

    /// <summary>
    /// Trainee package purchases GMV (100% belonging to Coaches via Escrow, 0% platform commission).
    /// </summary>
    public decimal TraineePackagesGMV { get; set; }

    /// <summary>
    /// Platform Net/Direct Revenue from Coach subscription plans (100% retained by FitSocial).
    /// </summary>
    public decimal PlatformDirectRevenue { get; set; }

    /// <summary>
    /// Total approved refund disbursements across all order types.
    /// </summary>
    public decimal TotalApprovedRefunds { get; set; }

    /// <summary>
    /// Total earnings disbursed to coaches via processed monthly payouts.
    /// </summary>
    public decimal TotalCoachPayouts { get; set; }

    /// <summary>
    /// Platform Net Cash Holding (Current platform cash in pool) = TotalGMV - TotalCoachPayouts - TotalApprovedRefunds.
    /// </summary>
    public decimal PlatformNetCashHolding { get; set; }

    /// <summary>
    /// Average Order Value across successful orders in the selected period.
    /// </summary>
    public decimal AverageOrderValue { get; set; }

    /// <summary>
    /// Count of successfully paid/completed orders.
    /// </summary>
    public int CompletedOrdersCount { get; set; }

    /// <summary>
    /// Total orders created (including pending/failed) in the period.
    /// </summary>
    public int TotalOrdersCount { get; set; }

    /// <summary>
    /// Previous period GMV for trend comparison.
    /// </summary>
    public decimal PreviousPeriodTotalGMV { get; set; }

    /// <summary>
    /// Previous period direct platform revenue.
    /// </summary>
    public decimal PreviousPeriodPlatformRevenue { get; set; }

    /// <summary>
    /// Growth rate percentage compared to previous period (+/- %).
    /// </summary>
    public double MoMGrowthRatePercentage { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class RevenueTimeSeriesPointDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal TotalGMV { get; set; }
    public decimal TraineePackageGMV { get; set; }
    public decimal CoachSubscriptionRevenue { get; set; }
    public decimal ApprovedRefunds { get; set; }
}

public class RevenueBreakdownDto
{
    public decimal TraineePackageSales { get; set; }
    public decimal CoachSubscriptionSales { get; set; }
    public double TraineePackagePercentage { get; set; }
    public double CoachSubscriptionPercentage { get; set; }
}

public class TopCoachRevenueDto
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

public class TopTrainingPackageDto
{
    public Guid PackageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class CoachSubscriptionPlanStatDto
{
    public Guid PlanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int SubscribersCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class FullRevenueAnalyticsResponseDto
{
    public RevenueOverviewDto Overview { get; set; } = new();
    public List<RevenueTimeSeriesPointDto> TimeSeries { get; set; } = new();
    public RevenueBreakdownDto Breakdown { get; set; } = new();
    public List<TopCoachRevenueDto> TopCoaches { get; set; } = new();
    public List<TopTrainingPackageDto> TopPackages { get; set; } = new();
    public List<CoachSubscriptionPlanStatDto> SubscriptionPlanStats { get; set; } = new();
}
