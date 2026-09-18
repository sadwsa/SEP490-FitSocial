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

    public Task<List<Sport>> ListAllWithCountsAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(s => s.Coaches)
            .Include(s => s.Users)
            .Include(s => s.Posts)
            .OrderBy(s => s.SportName)
            .ToListAsync(cancellationToken);
    }

    public Task<Sport?> GetByNameAsync(string sportName, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(s => s.SportName.ToLower() == sportName.Trim().ToLower(), cancellationToken);
    }

    public Task<Sport?> GetByNameExcludingIdAsync(string sportName, Guid sportId, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(s => s.SportId != sportId && s.SportName.ToLower() == sportName.Trim().ToLower(), cancellationToken);
    }

    public Task<Sport?> GetByIdWithDetailsAsync(Guid sportId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(s => s.Coaches)
            .Include(s => s.Users)
            .Include(s => s.Posts)
            .FirstOrDefaultAsync(s => s.SportId == sportId, cancellationToken);
    }
}
