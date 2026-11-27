using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

/// <summary>
/// High-performance repository for Coach Dashboard metrics.
/// All queries are read-only (.AsNoTracking()) and evaluated directly on PostgreSQL.
/// </summary>
public class CoachDashboardRepository : ICoachDashboardRepository
{
    private readonly FitSocialDbContext _context;

    private static readonly string[] PaidAnalyticsStatuses =
    {
        PaymentConstants.OrderStatusPaid,
        PaymentConstants.OrderStatusActive,
        PaymentConstants.OrderStatusCompleted
    };

    public CoachDashboardRepository(FitSocialDbContext context)
    {
        _context = context;
    }

    private IQueryable<Order> ValidAnalyticsOrders(Guid coachId)
    {
        return _context.Orders
            .AsNoTracking()
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && o.OrderType != PaymentConstants.OrderTypeCoachActivation
                        && o.OrderStatus != null
                        && PaidAnalyticsStatuses.Contains(o.OrderStatus.ToUpper())
                        && o.OrderStatus.ToUpper() != PaymentConstants.OrderStatusRefunded
                        && (o.RefundRequests == null || !o.RefundRequests.Any(r => r.Status != null && r.Status.ToUpper() == "APPROVED")));
    }

    /// <inheritdoc/>
    public async Task<int> GetActiveTraineesCountAsync(Guid coachId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Trainees (BuyerId) who have an order with status PAID/COMPLETED/ACTIVE for this coach
        // that is currently valid (not expired based on package duration) and not refunded.
        var orders = await ValidAnalyticsOrders(coachId)
            .Select(o => new
            {
                o.BuyerId,
                o.CreatedAt,
                DurationDays = o.OrderDetails.Select(od => od.PackageDurationDays).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return orders
            .Where(o => o.DurationDays == null
                        || o.DurationDays <= 0
                        || (o.CreatedAt.HasValue && o.CreatedAt.Value.AddDays((double)o.DurationDays.Value) >= now))
            .Select(o => o.BuyerId)
            .Distinct()
            .Count();
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalPackagesSoldAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        // Successful package orders count for this coach
        return await ValidAnalyticsOrders(coachId).CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetMonthlyRevenueAsync(Guid coachId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
    {
        // Total revenue from successful package orders created in the current month
        return await ValidAnalyticsOrders(coachId)
            .Where(o => o.CreatedAt.HasValue
                        && o.CreatedAt.Value >= startOfMonth
                        && o.CreatedAt.Value < endOfMonth)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
    }

    /// <inheritdoc/>
    public async Task<int> GetPendingRequestsCountAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        // 1. Pending orders for this coach
        var pendingOrders = await _context.Orders
            .AsNoTracking()
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && o.OrderStatus != null
                        && o.OrderStatus.ToUpper() == "PENDING")
            .CountAsync(cancellationToken);

        // 2. Pending or draft meal plans
        var pendingMealPlans = await _context.MealPlans
            .AsNoTracking()
            .Where(mp => mp.CoachId == coachId 
                         && mp.Status != null 
                         && (mp.Status.ToUpper() == "PENDING" || mp.Status.ToUpper() == "DRAFT" || mp.Status.ToUpper() == "WAITING"))
            .CountAsync(cancellationToken);

        // 3. Pending or draft training plans
        var pendingTrainingPlans = await _context.TrainingPlans
            .AsNoTracking()
            .Where(tp => tp.CoachId == coachId 
                         && tp.Status != null 
                         && (tp.Status.ToUpper() == "PENDING" || tp.Status.ToUpper() == "DRAFT" || tp.Status.ToUpper() == "WAITING"))
            .CountAsync(cancellationToken);

        // 4. Pending refund requests for this coach's orders
        var pendingRefunds = await _context.RefundRequests
            .AsNoTracking()
            .Where(r => r.Order != null 
                        && (r.Order.CoachId == coachId || r.Order.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && r.Status != null 
                        && r.Status.ToUpper() == "PENDING")
            .CountAsync(cancellationToken);

        return pendingOrders + pendingMealPlans + pendingTrainingPlans + pendingRefunds;
    }

    // Thêm các method này vào trong class CoachDashboardRepository

    public async Task<(decimal TotalRevenue, int TotalOrders, double RetentionRate)> GetAnalyticsSummaryAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var baseQuery = ValidAnalyticsOrders(coachId);

        // 1. Tổng doanh thu & Số đơn
        var summary = await baseQuery
            .GroupBy(o => 1)
            .Select(g => new
            {
                TotalRevenue = g.Sum(o => o.TotalAmount ?? 0),
                TotalOrders = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalRevenue = summary?.TotalRevenue ?? 0;
        var totalOrders = summary?.TotalOrders ?? 0;

        // 2. Retention Rate - Nhóm theo BuyerId đếm số order để kiểm tra retain >= 2
        var traineeOrderCounts = await baseQuery
            .GroupBy(o => o.BuyerId)
            .Select(g => g.Count())
            .ToListAsync(cancellationToken);

        var totalTrainees = traineeOrderCounts.Count;
        var retainedTrainees = traineeOrderCounts.Count(c => c >= 2);
        var retentionRate = totalTrainees > 0
            ? Math.Round((double)retainedTrainees / totalTrainees * 100, 1)
            : 0;

        return (totalRevenue, totalOrders, retentionRate);
    }

    public async Task<List<(int Year, int Month, decimal Revenue, int TotalOrders)>> GetMonthlyStatsAsync(Guid coachId, DateTime startDate, CancellationToken cancellationToken = default)
    {
        var orders = await ValidAnalyticsOrders(coachId)
            .Where(o => o.CreatedAt != null && o.CreatedAt >= startDate)
            .Select(o => new
            {
                CreatedAt = o.CreatedAt!.Value,
                TotalAmount = o.TotalAmount ?? 0
            })
            .ToListAsync(cancellationToken);

        // Phân chia tháng theo múi giờ Việt Nam (UTC+7)
        var stats = orders
            .GroupBy(o =>
            {
                var vnTime = o.CreatedAt.AddHours(7);
                return new { vnTime.Year, vnTime.Month };
            })
            .Select(g => (
                Year: g.Key.Year,
                Month: g.Key.Month,
                Revenue: g.Sum(o => o.TotalAmount),
                TotalOrders: g.Count()
            ))
            .ToList();

        return stats;
    }

    public async Task<List<(Guid TraineeId, string TraineeName, decimal TotalSpent, int TotalOrders)>> GetTopTraineesAsync(Guid coachId, int limit, CancellationToken cancellationToken = default)
    {
        var topTrainees = await ValidAnalyticsOrders(coachId)
            .GroupBy(o => new { o.BuyerId, o.Buyer.FullName }) // Entity Framework Core join bảng User tự động
            .Select(g => new
            {
                TraineeId = g.Key.BuyerId,
                TraineeName = g.Key.FullName ?? "Unknown User",
                TotalSpent = g.Sum(o => o.TotalAmount ?? 0),
                TotalOrders = g.Count()
            })
            .OrderByDescending(t => t.TotalSpent)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return topTrainees.Select(t => (t.TraineeId, t.TraineeName, t.TotalSpent, t.TotalOrders)).ToList();
    }

}
