using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PriceRepository : Repository<Price>, IPriceRepository
{
    public PriceRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Price?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(p => p.IsActive == true)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
