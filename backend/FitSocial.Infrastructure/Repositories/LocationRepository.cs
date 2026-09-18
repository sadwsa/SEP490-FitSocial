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
}
