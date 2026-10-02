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

    public Task<CoachSubscriptionPlan?> GetActivePlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(p => p.CoachSubscriptionPlansId == id && p.IsActive == true, cancellationToken);
    }

    public Task<(List<SubscriptionPriceHistoryItem> Items, int TotalCount)> GetPriceHistoryAsync(
        Guid? planId,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var historyRepo = new SubscriptionPriceHistoryRepository(DbContext);
        return historyRepo.GetPriceHistoryAsync(planId, keyword, fromDate, toDate, pageIndex, pageSize, cancellationToken);
    }
}

