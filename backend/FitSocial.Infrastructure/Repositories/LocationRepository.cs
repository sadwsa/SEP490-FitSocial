using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class LocationRepository : Repository<Location>, ILocationRepository
{
    public LocationRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Location>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        return DbSet.OrderBy(l => l.LocationName).ToListAsync(cancellationToken);
    }

    public async Task<(List<Location> Items, int TotalCount)> ListLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var clean = searchTerm.Trim();
            if (clean.StartsWith("#"))
            {
                clean = clean[1..].Trim();
            }

            if (!string.IsNullOrWhiteSpace(clean))
            {
                var term = $"%{clean}%";
                query = query.Where(l =>
                    EF.Functions.ILike(l.LocationName, term) ||
                    (l.Address != null && EF.Functions.ILike(l.Address, term)) ||
                    EF.Functions.ILike(l.LocationId.ToString(), term));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var items = await query
            .Include(l => l.Posts)
            .OrderBy(l => l.LocationName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Location?> GetByIdWithDetailsAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(l => l.Posts)
            .Include(l => l.Coaches)
            .FirstOrDefaultAsync(l => l.LocationId == locationId, cancellationToken);
    }
}
