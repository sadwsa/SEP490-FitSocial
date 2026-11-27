using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class MealPlanRepository : IMealPlanRepository
{
    private readonly FitSocialDbContext _context;

    public MealPlanRepository(FitSocialDbContext context)
    {
        _context = context;
    }

    public async Task<MealPlan?> GetMealPlanByOrderDetailsAsync(
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        return await _context.MealPlans
            .AsSplitQuery()
            .AsNoTracking()
            .Include(mp => mp.MealPlanItems.OrderBy(i => i.WeekNumber).ThenBy(i => i.DayNumber).ThenBy(i => i.SortOrder))
                .ThenInclude(i => i.MealCompletionLogs)
            .Include(mp => mp.Coach)
                .ThenInclude(c => c.Coach)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Package)
                    .ThenInclude(p => p!.Media)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Order)
                    .ThenInclude(o => o!.Buyer)
            .Where(mp => mp.OrderDetailsId == orderDetailsId && mp.Status != "DELETED")
            .OrderByDescending(mp => mp.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MealPlan?> GetByIdWithItemsAsync(
        Guid mealPlanId,
        bool asNoTracking = false,
        CancellationToken ct = default)
    {
        var query = _context.MealPlans
            .Include(mp => mp.MealPlanItems)
            .Include(mp => mp.Coach)
                .ThenInclude(c => c.Coach)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Package)
                    .ThenInclude(p => p!.Media)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Order)
            .Where(mp => mp.MealPlanId == mealPlanId && mp.Status != "DELETED");

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<OrderDetail?> GetOrderDetailWithOrderAndPackageAsync(
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        return await _context.OrderDetails
            .Include(od => od.Order)
                .ThenInclude(o => o!.Buyer)
            .Include(od => od.Package)
                .ThenInclude(p => p!.Coach)
                    .ThenInclude(c => c!.Coach)
            .FirstOrDefaultAsync(od => od.OrderDetailsId == orderDetailsId, ct);
    }

    public async Task AddAsync(MealPlan mealPlan, CancellationToken ct = default)
    {
        await _context.MealPlans.AddAsync(mealPlan, ct);
    }

    public void RemoveItems(IEnumerable<MealPlanItem> items)
    {
        _context.MealPlanItems.RemoveRange(items);
    }
    public async Task AddItemAsync(MealPlanItem item, CancellationToken ct = default)
    {
        await _context.MealPlanItems.AddAsync(item, ct);
    }

    public async Task<MealPlanItem?> GetMealItemByIdAsync(Guid mealPlanItemId, CancellationToken ct = default)
    {
        return await _context.MealPlanItems
            .Include(i => i.MealPlan)
                .ThenInclude(mp => mp.Coach)
                    .ThenInclude(c => c.Coach)
            .Include(i => i.MealPlan)
                .ThenInclude(mp => mp.OrderDetails)
                    .ThenInclude(od => od!.Order)
                        .ThenInclude(o => o!.Buyer)
            .Include(i => i.MealPlan)
                .ThenInclude(mp => mp.OrderDetails)
                    .ThenInclude(od => od!.Package)
            .FirstOrDefaultAsync(i => i.MealPlanItemId == mealPlanItemId, ct);
    }

    public async Task<MealCompletionLog?> GetMealLogAsync(Guid mealPlanItemId, Guid traineeId, CancellationToken ct = default)
    {
        return await _context.MealCompletionLogs
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(l => l.MealPlanItemId == mealPlanItemId && l.TraineeId == traineeId, ct);
    }

    public async Task AddMealLogAsync(MealCompletionLog log, CancellationToken ct = default)
    {
        await _context.MealCompletionLogs.AddAsync(log, ct);
    }

    public async Task<MealPlan?> GetCoachMealPlanWithLogsAsync(Guid orderDetailsId, Guid coachId, CancellationToken ct = default)
    {
        return await _context.MealPlans
            .AsSplitQuery()
            .AsNoTracking()
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Order)
                    .ThenInclude(o => o!.Buyer)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Package)
            .Include(mp => mp.MealPlanItems.OrderBy(i => i.WeekNumber).ThenBy(i => i.DayNumber).ThenBy(i => i.SortOrder))
                .ThenInclude(i => i.MealCompletionLogs)
            .Where(mp => mp.OrderDetailsId == orderDetailsId && mp.Status != "DELETED" &&
                (mp.CoachId == coachId
                 || (mp.OrderDetails != null && mp.OrderDetails.Order != null && mp.OrderDetails.Order.CoachId == coachId)
                 || (mp.OrderDetails != null && mp.OrderDetails.Package != null && mp.OrderDetails.Package.CoachId == coachId)))
            .OrderByDescending(mp => mp.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
