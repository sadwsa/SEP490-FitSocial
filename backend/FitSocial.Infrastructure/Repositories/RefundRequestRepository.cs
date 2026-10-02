using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class RefundRequestRepository : Repository<RefundRequest>, IRefundRequestRepository
{
    public RefundRequestRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<RefundRequest>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(r => r.Order)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Where(r => r.RequestedBy == userId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<List<RefundRequest>> ListAllWithDetailsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(r => r.Order)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.Status != null && r.Status.ToUpper() == s);
        }

        return query
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<RefundRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(r => r.Order)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.RefundRequestId == id, cancellationToken);
    }

    public Task<bool> HasPendingRefundAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AnyAsync(r => r.OrderId == orderId && (r.Status == "PENDING" || r.Status == "IN_REVIEW"), cancellationToken);
    }
}
