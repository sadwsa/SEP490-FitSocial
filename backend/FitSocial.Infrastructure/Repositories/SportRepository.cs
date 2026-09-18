using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class SportRepository : Repository<Sport>, ISportRepository
{
    public SportRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Sport>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return DbSet.OrderBy(s => s.SportName).ToListAsync(cancellationToken);
    }

    public Task<List<Sport>> ListByIdsAsync(IEnumerable<Guid> sportIds, CancellationToken cancellationToken = default)
    {
        var ids = sportIds.Distinct().ToList();
        return DbSet.Where(s => ids.Contains(s.SportId)).ToListAsync(cancellationToken);
    }
}
