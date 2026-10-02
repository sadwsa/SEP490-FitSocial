using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CartRepository : Repository<Cart>, ICartRepository
{
    public CartRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Cart?> GetCartItemAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Package)
                .ThenInclude(p => p.Coach)
                    .ThenInclude(cp => cp.Coach)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.PackageId == packageId, cancellationToken);
    }

    public async Task<IEnumerable<Cart>> GetCartByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Package)
                .ThenInclude(p => p.Coach)
                    .ThenInclude(cp => cp.Coach)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCartCountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.Quantity, cancellationToken);
    }
}
