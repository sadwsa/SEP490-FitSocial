using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Models.Analytics;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class RevenueAnalyticsRepository : IRevenueAnalyticsRepository
{
    private readonly FitSocialDbContext _context;

    public RevenueAnalyticsRepository(FitSocialDbContext context)
    {
        _context = context;
    }

    private static bool IsPackagePaid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim().ToUpperInvariant();
        return s is "PAID" or "COMPLETED" or "ACTIVE";
    }

    private static bool IsCoachPlanPaid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        var s = status.Trim().ToUpperInvariant();
        return s is "PAID" or "COMPLETED" or "ACTIVE";
    }

    public async Task<RevenueOverviewRawModel> GetRevenueOverviewAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

        // 1. Orders within the timeframe
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value >= fromDate && o.CreatedAt.Value <= toDate)
            .Select(o => new
            {
                o.OrderId,
                o.TotalAmount,
                o.OrderStatus,
                o.OrderType
            })
            .ToListAsync(ct);

        var packageOrders = orders
            .Where(o => (o.OrderType == null || string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
                        && IsPackagePaid(o.OrderStatus))
            .ToList();

        var coachOrders = orders
            .Where(o => string.Equals(o.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase)
                        && IsCoachPlanPaid(o.OrderStatus))
            .ToList();

        var traineePackagesGMV = packageOrders.Sum(o => o.TotalAmount ?? 0m);
        var coachSubscriptionsGMV = coachOrders.Sum(o => o.TotalAmount ?? 0m);
        var totalGMV = traineePackagesGMV + coachSubscriptionsGMV;
        var completedOrdersCount = packageOrders.Count + coachOrders.Count;

        // 2. Approved Refunds
        var approvedRefunds = await _context.RefundRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Where(r => r.Status != null && r.Status.ToUpper() == "APPROVED"
                && ((r.RefundedAt.HasValue && r.RefundedAt.Value >= fromDate && r.RefundedAt.Value <= toDate)
                    || (!r.RefundedAt.HasValue && r.ReviewedAt.HasValue && r.ReviewedAt.Value >= fromDate && r.ReviewedAt.Value <= toDate)
                    || (!r.RefundedAt.HasValue && !r.ReviewedAt.HasValue && r.CreatedAt.HasValue && r.CreatedAt.Value >= fromDate && r.CreatedAt.Value <= toDate)))
            .Select(r => new
            {
                r.ApprovedAmount,
                r.RequestedAmount,
                OrderType = r.Order != null ? r.Order.OrderType : null
            })
            .ToListAsync(ct);

        var totalApprovedRefunds = approvedRefunds.Sum(r => r.ApprovedAmount ?? r.RequestedAmount ?? 0m);
        var coachSubscriptionRefunds = approvedRefunds
            .Where(r => string.Equals(r.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.ApprovedAmount ?? r.RequestedAmount ?? 0m);

        // 3. Processed Payouts
        var totalCoachPayouts = await _context.Payouts
            .AsNoTracking()
            .Where(p => p.Status != null && p.Status.ToUpper() == "PROCESSED"
                && ((p.ProcessedAt.HasValue && p.ProcessedAt.Value >= fromDate && p.ProcessedAt.Value <= toDate)
                    || (!p.ProcessedAt.HasValue && p.CreatedAt.HasValue && p.CreatedAt.Value >= fromDate && p.CreatedAt.Value <= toDate)))
            .SumAsync(p => (decimal?)p.NetPayoutAmount, ct) ?? 0m;

        return new RevenueOverviewRawModel
        {
            TotalGMV = totalGMV,
            TraineePackagesGMV = traineePackagesGMV,
            CoachSubscriptionsGMV = coachSubscriptionsGMV,
            CoachSubscriptionRefunds = coachSubscriptionRefunds,
            TotalApprovedRefunds = totalApprovedRefunds,
            TotalCoachPayouts = totalCoachPayouts,
            CompletedOrdersCount = completedOrdersCount,
            TotalOrdersCount = orders.Count
        };
    }

    public async Task<List<RevenueTimeSeriesPointModel>> GetRevenueTimeSeriesAsync(
        DateTime fromDate,
        DateTime toDate,
        string granularity,
        CancellationToken ct = default)
    {
        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

        // 1. Fetch orders in timeframe
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value >= fromDate && o.CreatedAt.Value <= toDate)
            .Select(o => new
            {
                Date = o.CreatedAt!.Value,
                o.TotalAmount,
                o.OrderStatus,
                o.OrderType
            })
            .ToListAsync(ct);

        // 2. Fetch approved refunds in timeframe
        var refunds = await _context.RefundRequests
            .AsNoTracking()
            .Where(r => r.Status != null && r.Status.ToUpper() == "APPROVED"
                && ((r.RefundedAt.HasValue && r.RefundedAt.Value >= fromDate && r.RefundedAt.Value <= toDate)
                    || (!r.RefundedAt.HasValue && r.ReviewedAt.HasValue && r.ReviewedAt.Value >= fromDate && r.ReviewedAt.Value <= toDate)
                    || (!r.RefundedAt.HasValue && !r.ReviewedAt.HasValue && r.CreatedAt.HasValue && r.CreatedAt.Value >= fromDate && r.CreatedAt.Value <= toDate)))
            .Select(r => new
            {
                Date = r.RefundedAt ?? r.ReviewedAt ?? r.CreatedAt!.Value,
                Amount = r.ApprovedAmount ?? r.RequestedAmount ?? 0m
            })
            .ToListAsync(ct);

        granularity = granularity.ToUpperInvariant();

        // Build continuous buckets
        var result = new List<RevenueTimeSeriesPointModel>();

        if (granularity == "DAILY")
        {
            var cur = fromDate.Date;
            var endDate = toDate.Date;
            while (cur <= endDate)
            {
                var next = cur.AddDays(1);
                var bucketOrders = orders.Where(o => o.Date >= cur && o.Date < next).ToList();
                var bucketRefunds = refunds.Where(r => r.Date >= cur && r.Date < next).ToList();

                var pkgGMV = bucketOrders
                    .Where(o => (o.OrderType == null || string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase)) && IsPackagePaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var coachRev = bucketOrders
                    .Where(o => string.Equals(o.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase) && IsCoachPlanPaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var refTotal = bucketRefunds.Sum(r => r.Amount);

                result.Add(new RevenueTimeSeriesPointModel
                {
                    PeriodKey = cur.ToString("dd/MM"),
                    Date = cur,
                    TotalGMV = pkgGMV + coachRev,
                    TraineePackageGMV = pkgGMV,
                    CoachSubscriptionRevenue = coachRev,
                    ApprovedRefunds = refTotal
                });

                cur = next;
            }
        }
        else if (granularity == "WEEKLY")
        {
            var cur = fromDate.Date;
            while (cur <= toDate)
            {
                var next = cur.AddDays(7);
                var bucketOrders = orders.Where(o => o.Date >= cur && o.Date < next).ToList();
                var bucketRefunds = refunds.Where(r => r.Date >= cur && r.Date < next).ToList();

                var pkgGMV = bucketOrders
                    .Where(o => (o.OrderType == null || string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase)) && IsPackagePaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var coachRev = bucketOrders
                    .Where(o => string.Equals(o.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase) && IsCoachPlanPaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var refTotal = bucketRefunds.Sum(r => r.Amount);

                result.Add(new RevenueTimeSeriesPointModel
                {
                    PeriodKey = $"{cur:dd/MM} - {cur.AddDays(6):dd/MM}",
                    Date = cur,
                    TotalGMV = pkgGMV + coachRev,
                    TraineePackageGMV = pkgGMV,
                    CoachSubscriptionRevenue = coachRev,
                    ApprovedRefunds = refTotal
                });

                cur = next;
            }
        }
        else // MONTHLY
        {
            var cur = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endMonth = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            while (cur <= endMonth)
            {
                var next = cur.AddMonths(1);
                var bucketOrders = orders.Where(o => o.Date >= cur && o.Date < next).ToList();
                var bucketRefunds = refunds.Where(r => r.Date >= cur && r.Date < next).ToList();

                var pkgGMV = bucketOrders
                    .Where(o => (o.OrderType == null || string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase)) && IsPackagePaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var coachRev = bucketOrders
                    .Where(o => string.Equals(o.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase) && IsCoachPlanPaid(o.OrderStatus))
                    .Sum(o => o.TotalAmount ?? 0m);

                var refTotal = bucketRefunds.Sum(r => r.Amount);

                result.Add(new RevenueTimeSeriesPointModel
                {
                    PeriodKey = cur.ToString("MM/yyyy"),
                    Date = cur,
                    TotalGMV = pkgGMV + coachRev,
                    TraineePackageGMV = pkgGMV,
                    CoachSubscriptionRevenue = coachRev,
                    ApprovedRefunds = refTotal
                });

                cur = next;
            }
        }

        return result;
    }

    public async Task<RevenueBreakdownModel> GetRevenueBreakdownAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var overview = await GetRevenueOverviewAsync(fromDate, toDate, ct);
        var total = overview.TraineePackagesGMV + overview.CoachSubscriptionsGMV;

        return new RevenueBreakdownModel
        {
            TraineePackageSales = overview.TraineePackagesGMV,
            CoachSubscriptionSales = overview.CoachSubscriptionsGMV,
            TraineePackagePercentage = total > 0 ? Math.Round((double)(overview.TraineePackagesGMV / total) * 100, 1) : 0,
            CoachSubscriptionPercentage = total > 0 ? Math.Round((double)(overview.CoachSubscriptionsGMV / total) * 100, 1) : 0
        };
    }

    public async Task<List<TopCoachRevenueModel>> GetTopCoachesByGrossSalesAsync(
        DateTime fromDate,
        DateTime toDate,
        int topCount,
        CancellationToken ct = default)
    {
        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

        // 1. Group package orders by coach
        var rawCoachSales = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CoachId.HasValue
                && o.CreatedAt.HasValue
                && o.CreatedAt.Value >= fromDate
                && o.CreatedAt.Value <= toDate
                && (o.OrderType == null || o.OrderType == PaymentConstants.OrderTypePackage)
                && o.OrderStatus != null
                && (o.OrderStatus.ToUpper() == "PAID" || o.OrderStatus.ToUpper() == "COMPLETED" || o.OrderStatus.ToUpper() == "ACTIVE"))
            .GroupBy(o => o.CoachId!.Value)
            .Select(g => new
            {
                CoachId = g.Key,
                GrossSales = g.Sum(o => o.TotalAmount ?? 0m),
                CompletedOrdersCount = g.Count()
            })
            .OrderByDescending(x => x.GrossSales)
            .Take(topCount)
            .ToListAsync(ct);

        if (rawCoachSales.Count == 0)
        {
            return new List<TopCoachRevenueModel>();
        }

        var coachIds = rawCoachSales.Select(x => x.CoachId).ToList();

        // 2. Fetch coach profiles and user info
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => coachIds.Contains(u.UserId))
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.AvatarUrl
            })
            .ToDictionaryAsync(u => u.UserId, ct);

        // 3. Fetch payouts disbursed to these coaches
        var payouts = await _context.Payouts
            .AsNoTracking()
            .Where(p => coachIds.Contains(p.CoachId) && p.Status != null && p.Status.ToUpper() == "PROCESSED")
            .GroupBy(p => p.CoachId)
            .Select(g => new
            {
                CoachId = g.Key,
                TotalPayouts = g.Sum(p => p.NetPayoutAmount ?? 0m)
            })
            .ToDictionaryAsync(x => x.CoachId, x => x.TotalPayouts, ct);

        // 4. Fetch approved refunds for these coaches' orders
        var refunds = await _context.RefundRequests
            .AsNoTracking()
            .Include(r => r.Order)
            .Where(r => r.Status != null && r.Status.ToUpper() == "APPROVED"
                && r.Order != null && r.Order.CoachId.HasValue && coachIds.Contains(r.Order.CoachId.Value))
            .GroupBy(r => r.Order!.CoachId!.Value)
            .Select(g => new
            {
                CoachId = g.Key,
                RefundAmount = g.Sum(r => r.ApprovedAmount ?? r.RequestedAmount ?? 0m)
            })
            .ToDictionaryAsync(x => x.CoachId, x => x.RefundAmount, ct);

        return rawCoachSales.Select(x =>
        {
            users.TryGetValue(x.CoachId, out var user);
            payouts.TryGetValue(x.CoachId, out var totalPayout);
            refunds.TryGetValue(x.CoachId, out var refAmount);

            return new TopCoachRevenueModel
            {
                CoachId = x.CoachId,
                CoachName = user?.FullName ?? "Unknown Coach",
                CoachEmail = user?.Email ?? string.Empty,
                AvatarUrl = user?.AvatarUrl,
                GrossSales = x.GrossSales,
                CompletedOrdersCount = x.CompletedOrdersCount,
                TotalPayouts = totalPayout,
                RefundAmount = refAmount
            };
        }).ToList();
    }

    public async Task<List<TopTrainingPackageModel>> GetTopSellingPackagesAsync(
        DateTime fromDate,
        DateTime toDate,
        int topCount,
        CancellationToken ct = default)
    {
        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

        var rawList = await _context.OrderDetails
            .AsNoTracking()
            .Where(d => d.PackageId.HasValue
                && d.Order != null
                && d.Order.CreatedAt.HasValue
                && d.Order.CreatedAt.Value >= fromDate
                && d.Order.CreatedAt.Value <= toDate
                && (d.Order.OrderType == null || d.Order.OrderType == PaymentConstants.OrderTypePackage)
                && d.Order.OrderStatus != null
                && (d.Order.OrderStatus.ToUpper() == "PAID" || d.Order.OrderStatus.ToUpper() == "COMPLETED" || d.Order.OrderStatus.ToUpper() == "ACTIVE"))
            .Select(d => new
            {
                PackageId = d.PackageId!.Value,
                Title = d.PackageTitle,
                CoachName = d.CoachName,
                Price = d.PackagePrice ?? 0m
            })
            .ToListAsync(ct);

        if (rawList.Count == 0)
        {
            return new List<TopTrainingPackageModel>();
        }

        return rawList
            .GroupBy(d => d.PackageId)
            .Select(g =>
            {
                var first = g.First();
                return new TopTrainingPackageModel
                {
                    PackageId = g.Key,
                    Title = first.Title ?? "Training Package",
                    CoachName = first.CoachName ?? "Coach",
                    Price = first.Price,
                    TotalSold = g.Count(),
                    TotalRevenue = g.Sum(x => x.Price)
                };
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(topCount)
            .ToList();
    }

    public async Task<List<CoachSubscriptionPlanStatModel>> GetCoachSubscriptionPlansStatsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        fromDate = DateTime.SpecifyKind(fromDate, DateTimeKind.Unspecified);
        toDate = DateTime.SpecifyKind(toDate, DateTimeKind.Unspecified);

        var plans = await _context.CoachSubscriptionPlans
            .AsNoTracking()
            .ToListAsync(ct);

        var details = await _context.OrderDetails
            .AsNoTracking()
            .Include(d => d.Order)
            .Where(d => d.CoachSubscriptionPlansId.HasValue
                && d.Order != null
                && d.Order.CreatedAt.HasValue
                && d.Order.CreatedAt.Value >= fromDate
                && d.Order.CreatedAt.Value <= toDate
                && d.Order.OrderType == PaymentConstants.OrderTypeCoachActivation
                && d.Order.OrderStatus != null
                && (d.Order.OrderStatus.ToUpper() == "PAID" || d.Order.OrderStatus.ToUpper() == "COMPLETED" || d.Order.OrderStatus.ToUpper() == "ACTIVE"))
            .GroupBy(d => d.CoachSubscriptionPlansId!.Value)
            .Select(g => new
            {
                PlanId = g.Key,
                SubscribersCount = g.Count(),
                TotalRevenue = g.Sum(d => d.PackagePrice ?? 0m)
            })
            .ToDictionaryAsync(x => x.PlanId, ct);

        return plans.Select(p =>
        {
            details.TryGetValue(p.CoachSubscriptionPlansId, out var stat);
            return new CoachSubscriptionPlanStatModel
            {
                PlanId = p.CoachSubscriptionPlansId,
                Description = p.Description ?? "Coach Plan",
                Price = p.Amount ?? 0m,
                DurationDays = p.SubscriptionDuration ?? p.TrainingPackageDuration ?? 30,
                SubscribersCount = stat?.SubscribersCount ?? 0,
                TotalRevenue = stat?.TotalRevenue ?? 0m
            };
        })
        .OrderByDescending(x => x.TotalRevenue)
        .ToList();
    }
}
