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

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedLogsAsync(
        string? search,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.ActorAccount)
            .AsQueryable();

        // 1. Filter by Date range
        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        // 2. Filter by Source (EntityType)
        if (!string.IsNullOrWhiteSpace(source))
        {
            var s = source.Trim().ToLower();
            query = query.Where(a => a.EntityType != null && a.EntityType.ToLower() == s);
        }

        // 3. Filter by Level
        if (!string.IsNullOrWhiteSpace(level))
        {
            var lvl = level.Trim().ToUpperInvariant();
            if (lvl == "ERROR" || lvl == "CRITICAL")
            {
                query = query.Where(a => a.Action != null && (
                    a.Action.ToUpper().Contains("ERROR") ||
                    a.Action.ToUpper().Contains("EXCEPTION") ||
                    a.Action.ToUpper().Contains("FAIL") ||
                    a.Action.ToUpper().Contains("CRITICAL")));
            }
            else if (lvl == "WARNING")
            {
                query = query.Where(a => a.Action != null && (
                    a.Action.ToUpper().Contains("REJECT") ||
                    a.Action.ToUpper().Contains("WARN") ||
                    a.Action.ToUpper().Contains("LOCK") ||
                    a.Action.ToUpper().Contains("SUSPEND") ||
                    a.Action.ToUpper().Contains("CANCEL") ||
                    a.Action.ToUpper().Contains("REVOKE") ||
                    a.Action.ToUpper().Contains("DELETE")));
            }
            else if (lvl == "INFORMATION" || lvl == "INFO")
            {
                query = query.Where(a => a.Action == null || (!a.Action.ToUpper().Contains("ERROR") &&
                    !a.Action.ToUpper().Contains("EXCEPTION") &&
                    !a.Action.ToUpper().Contains("FAIL") &&
                    !a.Action.ToUpper().Contains("CRITICAL") &&
                    !a.Action.ToUpper().Contains("REJECT") &&
                    !a.Action.ToUpper().Contains("WARN") &&
                    !a.Action.ToUpper().Contains("LOCK") &&
                    !a.Action.ToUpper().Contains("SUSPEND") &&
                    !a.Action.ToUpper().Contains("CANCEL") &&
                    !a.Action.ToUpper().Contains("REVOKE") &&
                    !a.Action.ToUpper().Contains("DELETE")));
            }
        }

        // 4. Search by keyword
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(a =>
                (a.Action != null && a.Action.ToLower().Contains(kw)) ||
                (a.EntityType != null && a.EntityType.ToLower().Contains(kw)) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(kw)) ||
                (a.Ipaddress != null && a.Ipaddress.ToLower().Contains(kw)) ||
                (a.ActorAccount != null && a.ActorAccount.FullName != null && a.ActorAccount.FullName.ToLower().Contains(kw)) ||
                (a.ActorAccount != null && a.ActorAccount.Email != null && a.ActorAccount.Email.ToLower().Contains(kw)) ||
                (a.OldValue != null && a.OldValue.ToLower().Contains(kw)) ||
                (a.NewValue != null && a.NewValue.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AuditLogId)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<AuditLog?> GetByIdWithActorAsync(long logId, CancellationToken cancellationToken = default)
    {
        return await DbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.ActorAccount)
            .FirstOrDefaultAsync(a => a.AuditLogId == logId, cancellationToken);
    }
}
