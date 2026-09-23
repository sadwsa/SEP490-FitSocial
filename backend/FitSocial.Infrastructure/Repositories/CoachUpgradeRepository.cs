using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachUpgradeRepository : Repository<CoachUpgrade>, ICoachUpgradeRepository
{
    public CoachUpgradeRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public async Task<Dictionary<Guid, (int Total, int Active)>> GetCountsByPriceIdsAsync(IEnumerable<Guid> priceIds, CancellationToken cancellationToken = default)
    {
        var ids = priceIds.ToList();
        if (!ids.Any()) return new Dictionary<Guid, (int, int)>();
        var groups = await DbSet.Where(cu => ids.Contains(cu.PriceId))
            .GroupBy(cu => cu.PriceId)
            .Select(g => new { PriceId = g.Key, Total = g.Count(), Active = g.Count(x => x.Status == "ACTIVE" || x.Status == "SUCCESS") })
            .ToListAsync(cancellationToken);
        return groups.ToDictionary(x => x.PriceId, x => (x.Total, x.Active));
    }

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cancellationToken);

    public Task<int> CountByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cu => cu.PriceId == priceId, cancellationToken);

    public Task<int> CountActiveByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cu => cu.PriceId == priceId && (cu.Status == "ACTIVE" || cu.Status == "SUCCESS"), cancellationToken);
}
