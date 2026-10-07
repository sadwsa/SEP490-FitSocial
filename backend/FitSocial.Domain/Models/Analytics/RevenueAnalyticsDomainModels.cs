using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Models.Analytics;

public class RevenueOverviewRawModel
{
    public decimal TotalGMV { get; set; }
    public decimal TraineePackagesGMV { get; set; }
    public decimal CoachSubscriptionsGMV { get; set; }
    public decimal CoachSubscriptionRefunds { get; set; }
    public decimal TotalApprovedRefunds { get; set; }
    public decimal TotalCoachPayouts { get; set; }
    public int CompletedOrdersCount { get; set; }
    public int TotalOrdersCount { get; set; }
}

public class RevenueTimeSeriesPointModel
{
    public string PeriodKey { get; set; } = string.Empty;
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
