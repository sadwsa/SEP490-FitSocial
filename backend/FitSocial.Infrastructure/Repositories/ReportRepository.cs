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

public class ReportRepository : Repository<Report>, IReportRepository
{
    public ReportRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<(List<Report> Items, int TotalCount)> GetPagedReportsAsync(
        string? status,
        string? targetType,
        Guid? reporterId,
        DateTime? fromDate,
        DateTime? toDate,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpper();
            query = query.Where(r => r.Status != null && r.Status.ToUpper() == s);
        }

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            var t = targetType.Trim().ToUpper();
            if (t == "POST")
            {
                query = query.Where(r => r.ReportedPostId != null);
            }
            else if (t == "USER")
            {
                query = query.Where(r => r.ReportedUserId != null);
            }
        }

        if (reporterId.HasValue && reporterId.Value != Guid.Empty)
        {
            query = query.Where(r => r.ReporterId == reporterId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = $"%{searchTerm.Trim()}%";
            query = query.Where(r =>
                (r.Reason != null && EF.Functions.ILike(r.Reason, term)) ||
                (r.Reporter != null && ((r.Reporter.FullName != null && EF.Functions.ILike(r.Reporter.FullName, term)) || EF.Functions.ILike(r.Reporter.Email, term))) ||
                (r.ReportedUser != null && ((r.ReportedUser.FullName != null && EF.Functions.ILike(r.ReportedUser.FullName, term)) || EF.Functions.ILike(r.ReportedUser.Email, term))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Reporter)
            .Include(r => r.ReportedUser)
            .Include(r => r.ReportedPost)
                .ThenInclude(p => p!.Author)
            .Include(r => r.ResolvedByNavigation)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
