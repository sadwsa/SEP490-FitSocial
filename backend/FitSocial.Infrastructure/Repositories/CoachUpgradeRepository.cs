using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachUpgradeRepository : Repository<CoachUpgrade>, ICoachUpgradeRepository
{
    public CoachUpgradeRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public Task<Dictionary<Guid, (int Total, int Active)>> GetCountsByPlanIdsAsync(IEnumerable<Guid> planIds, CancellationToken cancellationToken = default)
        => GetCountsByPriceIdsAsync(planIds, cancellationToken);

    public async Task<Dictionary<Guid, (int Total, int Active)>> GetCountsByPriceIdsAsync(IEnumerable<Guid> priceIds, CancellationToken cancellationToken = default)
    {
        var ids = priceIds.ToList();
        if (!ids.Any()) return new Dictionary<Guid, (int, int)>();
        var groups = await DbSet.Where(cu => ids.Contains(cu.CoachSubscriptionPlansId))
            .GroupBy(cu => cu.CoachSubscriptionPlansId)
            .Select(g => new { PriceId = g.Key, Total = g.Count(), Active = g.Count(x => x.Status == "ACTIVE" || x.Status == "SUCCESS") })
            .ToListAsync(cancellationToken);
        return groups.ToDictionary(x => x.PriceId, x => (x.Total, x.Active));
    }

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cancellationToken);

    public Task<int> CountByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cu => cu.CoachSubscriptionPlansId == planId, cancellationToken);

    public Task<int> CountByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default)
        => CountByPlanIdAsync(priceId, cancellationToken);

    public Task<int> CountActiveByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cu => cu.CoachSubscriptionPlansId == planId && (cu.Status == "ACTIVE" || cu.Status == "SUCCESS"), cancellationToken);

    public Task<int> CountActiveByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default)
        => CountActiveByPlanIdAsync(priceId, cancellationToken);

    public Task<CoachUpgrade?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(cu => cu.OrderId == orderId, cancellationToken);
}