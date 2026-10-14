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
            .AsNoTracking()
            .Include(mp => mp.MealPlanItems.OrderBy(i => i.WeekNumber).ThenBy(i => i.DayNumber).ThenBy(i => i.SortOrder))
            .Include(mp => mp.Coach)
                .ThenInclude(c => c.Coach)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Package)
                    .ThenInclude(p => p!.Media)
            .Include(mp => mp.OrderDetails)
                .ThenInclude(od => od!.Order)
            .Where(mp => mp.OrderDetailsId == orderDetailsId && mp.Status != "DELETED")
            .OrderByDescending(mp => mp.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
