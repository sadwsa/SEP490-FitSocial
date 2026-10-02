using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories
{
    public class CartRepository : Repository<Cart>, ICartRepository
    {
        public CartRepository(FitSocialDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<List<Cart>> GetCartByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(c => c.Package)
                    .ThenInclude(p => p.Coach)
                        .ThenInclude(cp => cp.Coach)
                .Include(c => c.Package)
                    .ThenInclude(p => p.Coach)
                        .ThenInclude(cp => cp.Sports)
                .Include(c => c.Package)
                    .ThenInclude(p => p.Coach)
                        .ThenInclude(cp => cp.Reviews)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<Cart?> GetCartItemAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(c => c.Package)
                    .ThenInclude(p => p.Coach)
                        .ThenInclude(cp => cp.Coach)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.PackageId == packageId, cancellationToken);
        }

        public async Task<int> GetCartCountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(c => c.UserId == userId)
                .CountAsync(cancellationToken);
        }

        public async Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var items = await DbSet
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);

            if (items.Count > 0)
            {
                DbSet.RemoveRange(items);
            }
        }
    }
}
