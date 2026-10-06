using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Models.Analytics;

namespace FitSocial.Domain.Interfaces;

/// <summary>
/// Domain repository interface for high-performance read-only analytics queries on PostgreSQL.
/// Supports UC_27: Admin View System Revenue Analytics.
/// </summary>
public interface IRevenueAnalyticsRepository
{
    /// <summary>
    /// Computes high-level aggregated numbers (GMV, Package sales, Coach subscriptions, Refunds, Payouts) within the period.
    /// </summary>
    Task<RevenueOverviewRawModel> GetRevenueOverviewAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    /// <summary>
    /// Computes time-series data grouped by the specified granularity (DAILY, WEEKLY, MONTHLY).
    /// </summary>
    Task<List<RevenueTimeSeriesPointModel>> GetRevenueTimeSeriesAsync(DateTime fromDate, DateTime toDate, string granularity, CancellationToken ct = default);

    /// <summary>
    /// Computes proportion breakdown between Trainee Package sales and Coach Subscriptions.
    /// </summary>
    Task<RevenueBreakdownModel> GetRevenueBreakdownAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    /// <summary>
    /// Retrieves top coaches ranked by gross package sales volume.
    /// </summary>
    Task<List<TopCoachRevenueModel>> GetTopCoachesByGrossSalesAsync(DateTime fromDate, DateTime toDate, int topCount, CancellationToken ct = default);

    /// <summary>
    /// Retrieves top selling training packages by sales volume and revenue.
    /// </summary>
    Task<List<TopTrainingPackageModel>> GetTopSellingPackagesAsync(DateTime fromDate, DateTime toDate, int topCount, CancellationToken ct = default);

    /// <summary>
    /// Retrieves sales and revenue performance of coach subscription plans.
    /// </summary>
    Task<List<CoachSubscriptionPlanStatModel>> GetCoachSubscriptionPlansStatsAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
