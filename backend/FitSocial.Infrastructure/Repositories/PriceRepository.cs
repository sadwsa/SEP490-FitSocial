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

    public async Task<(List<Price> Items, int TotalCount)> ListPagedAsync(bool? isActive, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = DbSet.AsNoTracking().AsQueryable();
        if (isActive.HasValue) q = q.Where(p => p.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => (p.Currency != null && p.Currency.ToLower().Contains(s)) || (p.Amount != null && p.Amount.ToString()!.Contains(s)) || (p.Description != null && p.Description.ToLower().Contains(s)));
        }
        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountActiveAsync(CancellationToken cancellationToken = default)
        => DbSet.CountAsync(p => p.IsActive == true, cancellationToken);

    public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
        => DbSet.CountAsync(cancellationToken);
}
