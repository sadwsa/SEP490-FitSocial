using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class CoachSubscriptionPlanRepository : Repository<CoachSubscriptionPlan>, ICoachSubscriptionPlanRepository
{
    public CoachSubscriptionPlanRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<CoachSubscriptionPlan>> ListActivePlansAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(p => p.IsActive == true)
            .OrderBy(p => p.Amount)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<List<CoachSubscriptionPlan>> GetAllPlansAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .OrderBy(p => p.Amount)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<CoachSubscriptionPlan> Items, int TotalCount, int ActiveCount)> GetPlansAsync(
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                (p.Currency != null && p.Currency.ToLower().Contains(s)) ||
                (p.Description != null && p.Description.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var activeCount = await DbSet.CountAsync(p => p.IsActive == true, cancellationToken);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, activeCount);
    }

    public Task<CoachSubscriptionPlan?> GetActivePlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(p => p.CoachSubscriptionPlansId == id && p.IsActive == true, cancellationToken);
    }

    public async Task<(int Upgrades, int OrderDetails)> CountReferencesAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var upgrades = await DbContext.Set<CoachUpgrade>().CountAsync(u => u.CoachSubscriptionPlansId == planId, cancellationToken);
        var details = await DbContext.Set<OrderDetail>().CountAsync(d => d.CoachSubscriptionPlansId == planId, cancellationToken);
        return (upgrades, details);
    }
}

