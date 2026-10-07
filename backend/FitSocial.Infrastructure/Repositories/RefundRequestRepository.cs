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
                .ThenInclude(o => o.OrderDetails)
                    .ThenInclude(od => od.Package)
            .Include(r => r.Order)
                .ThenInclude(o => o.Coach)
                    .ThenInclude(c => c!.Coach)
            .Include(r => r.Order)
                .ThenInclude(o => o.Buyer)
            .Include(r => r.Order)
                .ThenInclude(o => o.Payments)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Payment)
            .Include(r => r.Term)
            .Where(r => r.RequestedBy == userId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<List<RefundRequest>> ListAllWithDetailsAsync(string? status = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(r => r.Order)
                .ThenInclude(o => o.OrderDetails)
                    .ThenInclude(od => od.Package)
            .Include(r => r.Order)
                .ThenInclude(o => o.Coach)
                    .ThenInclude(c => c!.Coach)
            .Include(r => r.Order)
                .ThenInclude(o => o.Buyer)
            .Include(r => r.Order)
                .ThenInclude(o => o.Payments)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Payment)
            .Include(r => r.Term)
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

    public async Task<(List<RefundRequest> Items, int TotalCount)> GetPagedRefundRequestsAsync(
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = DbSet
            .Include(r => r.Order)
                .ThenInclude(o => o.OrderDetails)
                    .ThenInclude(od => od.Package)
            .Include(r => r.Order)
                .ThenInclude(o => o.Coach)
                    .ThenInclude(c => c!.Coach)
            .Include(r => r.Order)
                .ThenInclude(o => o.Buyer)
            .Include(r => r.Order)
                .ThenInclude(o => o.Payments)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Payment)
            .Include(r => r.Term)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            query = query.Where(r => r.Status != null && r.Status.ToUpper() == s);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => (r.RequestedAt ?? r.CreatedAt) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => (r.RequestedAt ?? r.CreatedAt) <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(r =>
                (r.RequestedByNavigation.FullName != null && r.RequestedByNavigation.FullName.ToLower().Contains(term))
                || (r.RequestedByNavigation.Email != null && r.RequestedByNavigation.Email.ToLower().Contains(term))
                || (r.Reason != null && r.Reason.ToLower().Contains(term))
                || (r.Order.OrderDetails.Any(od => od.PackageTitle != null && od.PackageTitle.ToLower().Contains(term)))
                || r.OrderId.ToString().ToLower().Contains(term)
                || r.RefundRequestId.ToString().ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<RefundRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(r => r.Order)
                .ThenInclude(o => o.OrderDetails)
                    .ThenInclude(od => od.Package)
            .Include(r => r.Order)
                .ThenInclude(o => o.Coach)
                    .ThenInclude(c => c!.Coach)
            .Include(r => r.Order)
                .ThenInclude(o => o.Buyer)
            .Include(r => r.Order)
                .ThenInclude(o => o.Payments)
            .Include(r => r.Order)
                .ThenInclude(o => o.PayoutItems)
                    .ThenInclude(pi => pi.Payout)
            .Include(r => r.RequestedByNavigation)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Payment)
            .Include(r => r.Term)
            .FirstOrDefaultAsync(r => r.RefundRequestId == id, cancellationToken);
    }

    public Task<bool> HasPendingRefundAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AnyAsync(r => r.OrderId == orderId && (r.Status == "PENDING" || r.Status == "IN_REVIEW"), cancellationToken);
    }

    public Task<bool> HasApprovedRefundAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AnyAsync(r => r.OrderId == orderId && r.Status == "APPROVED", cancellationToken);
    }
}
