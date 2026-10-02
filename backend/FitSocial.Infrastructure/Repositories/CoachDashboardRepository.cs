using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public CoachDashboardRepository(FitSocialDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<int> GetActiveTraineesCountAsync(Guid coachId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Trainees (BuyerId) who have an order with status PAID/COMPLETED/ACTIVE for this coach
        // that is currently valid (not expired based on package duration) and not refunded.
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && o.OrderStatus != null
                        && (o.OrderStatus.ToUpper() == "PAID" 
                            || o.OrderStatus.ToUpper() == "COMPLETED" 
                            || o.OrderStatus.ToUpper() == "ACTIVE")
                        && (o.RefundRequests == null || !o.RefundRequests.Any(r => r.Status != null && r.Status.ToUpper() == "APPROVED")))
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
        return await _context.Orders
            .AsNoTracking()
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && o.OrderStatus != null
                        && (o.OrderStatus.ToUpper() == "PAID" 
                            || o.OrderStatus.ToUpper() == "COMPLETED" 
                            || o.OrderStatus.ToUpper() == "ACTIVE"))
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetMonthlyRevenueAsync(Guid coachId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default)
    {
        // Total revenue from successful package orders created in the current month
        return await _context.Orders
            .AsNoTracking()
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                        && o.OrderStatus != null
                        && (o.OrderStatus.ToUpper() == "PAID" 
                            || o.OrderStatus.ToUpper() == "COMPLETED" 
                            || o.OrderStatus.ToUpper() == "ACTIVE")
                        && o.CreatedAt.HasValue
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
}
