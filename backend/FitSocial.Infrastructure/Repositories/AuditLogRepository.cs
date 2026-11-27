using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Constants;
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

    public Task<(List<AuditLog> Items, int TotalCount)> GetPagedLogsAsync(
        string? search,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        return GetPagedLogsAsync(
            search,
            level,
            source,
            fromDate,
            toDate,
            pageNumber,
            pageSize,
            role: null,
            actionType: null,
            status: null,
            sortOrder: null,
            cancellationToken: cancellationToken);
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedLogsAsync(
        string? search,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        string? role,
        string? actionType,
        string? status,
        string? sortOrder,
        CancellationToken cancellationToken = default)
    {
        var query = BuildAdminStaffBaseQuery();

        // 1. Filter by Date range
        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        // 2. Filter by Actor Role (ADMIN / STAFF)
        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToUpperInvariant();
            if (r == RoleConstants.Admin)
            {
                query = query.Where(a => a.ActorAccount != null &&
                    a.ActorAccount.RoleCode != null &&
                    a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Admin);
            }
            else if (r == RoleConstants.Staff)
            {
                query = query.Where(a => a.ActorAccount != null &&
                    ((a.ActorAccount.RoleCode != null && a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Staff) ||
                     (a.ActorAccount.RoleCode == null && a.ActorAccount.IsInternal == true)));
            }
        }

        // 3. Filter by Action Type (CREATE, UPDATE, DELETE, LOCK, UNLOCK, APPROVE, REJECT)
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var at = actionType.Trim().ToUpperInvariant();
            switch (at)
            {
                case "CREATE":
                    query = query.Where(a => a.Action != null &&
                        (a.Action.ToUpper().Contains("CREATE") || a.Action.ToUpper().Contains("ADD")));
                    break;
                case "UPDATE":
                    query = query.Where(a => a.Action != null &&
                        (a.Action.ToUpper().Contains("UPDATE") ||
                         a.Action.ToUpper().Contains("EDIT") ||
                         a.Action.ToUpper().Contains("TOGGLE") ||
                         a.Action.ToUpper().Contains("SET_DEFAULT") ||
                         a.Action.ToUpper().Contains("CHANGE_") ||
                         a.Action.ToUpper().Contains("PROCESS_")));
                    break;
                case "DELETE":
                    query = query.Where(a => a.Action != null &&
                        (a.Action.ToUpper().Contains("DELETE") || a.Action.ToUpper().Contains("REMOVE")));
                    break;
                case "LOCK":
                    query = query.Where(a => a.Action != null &&
                        a.Action.ToUpper().Contains("LOCK") &&
                        !a.Action.ToUpper().Contains("UNLOCK"));
                    break;
                case "UNLOCK":
                    query = query.Where(a => a.Action != null &&
                        a.Action.ToUpper().Contains("UNLOCK"));
                    break;
                case "APPROVE":
                    query = query.Where(a => a.Action != null &&
                        a.Action.ToUpper().Contains("APPROVE"));
                    break;
                case "REJECT":
                    query = query.Where(a => a.Action != null &&
                        (a.Action.ToUpper().Contains("REJECT") || a.Action.ToUpper().Contains("DISMISS")));
                    break;
                default:
                    query = query.Where(a => a.Action != null && a.Action.ToUpper().Contains(at));
                    break;
            }
        }

        // 4. Filter by Result Status (Success / Failed)
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToUpperInvariant();
            if (st == "FAILED" || st == "FAIL" || st == "ERROR")
            {
                query = query.Where(a =>
                    (a.Action != null && (
                        a.Action.ToUpper().Contains("ERROR") ||
                        a.Action.ToUpper().Contains("EXCEPTION") ||
                        a.Action.ToUpper().Contains("FAIL") ||
                        a.Action.ToUpper().Contains("CRITICAL"))) ||
                    (a.NewValue != null && (
                        a.NewValue.ToLower().Contains("\"status\":\"failed\"") ||
                        a.NewValue.ToLower().Contains("\"status\": \"failed\"") ||
                        a.NewValue.ToLower().Contains("\"success\":false") ||
                        a.NewValue.ToLower().Contains("\"success\": false"))));
            }
            else if (st == "SUCCESS" || st == "SUCCEEDED" || st == "OK")
            {
                query = query.Where(a =>
                    (a.Action == null || (
                        !a.Action.ToUpper().Contains("ERROR") &&
                        !a.Action.ToUpper().Contains("EXCEPTION") &&
                        !a.Action.ToUpper().Contains("FAIL") &&
                        !a.Action.ToUpper().Contains("CRITICAL"))) &&
                    (a.NewValue == null || (
                        !a.NewValue.ToLower().Contains("\"status\":\"failed\"") &&
                        !a.NewValue.ToLower().Contains("\"status\": \"failed\"") &&
                        !a.NewValue.ToLower().Contains("\"success\":false") &&
                        !a.NewValue.ToLower().Contains("\"success\": false"))));
            }
        }

        // 5. Filter by Source (EntityType)
        if (!string.IsNullOrWhiteSpace(source))
        {
            var s = source.Trim().ToLower();
            query = query.Where(a => a.EntityType != null && a.EntityType.ToLower() == s);
        }

        // 6. Filter by Level (Legacy compatibility)
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

        // 7. Search by keyword (actor name/email, action, affected entity, target ID, etc.)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            var normalizedActionKw = kw.Replace(' ', '_');
            query = query.Where(a =>
                (a.Action != null && (a.Action.ToLower().Contains(kw) || a.Action.ToLower().Contains(normalizedActionKw))) ||
                (a.EntityType != null && a.EntityType.ToLower().Contains(kw)) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(kw)) ||
                (a.Ipaddress != null && a.Ipaddress.ToLower().Contains(kw)) ||
                (a.ActorAccount != null && a.ActorAccount.FullName != null && a.ActorAccount.FullName.ToLower().Contains(kw)) ||
                (a.ActorAccount != null && a.ActorAccount.Email != null && a.ActorAccount.Email.ToLower().Contains(kw)) ||
                (a.ActorAccount != null && a.ActorAccount.RoleCode != null && a.ActorAccount.RoleCode.ToLower().Contains(kw)) ||
                (a.OldValue != null && a.OldValue.ToLower().Contains(kw)) ||
                (a.NewValue != null && a.NewValue.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);

        var isAscending = !string.IsNullOrWhiteSpace(sortOrder) &&
            (sortOrder.Trim().Equals("asc", StringComparison.OrdinalIgnoreCase) ||
             sortOrder.Trim().Equals("oldest", StringComparison.OrdinalIgnoreCase));

        var orderedQuery = isAscending
            ? query.OrderBy(a => a.CreatedAt).ThenBy(a => a.AuditLogId)
            : query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuditLogId);

        var items = await orderedQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        await EnrichLogsWithEntityNamesAsync(items, cancellationToken);

        return (items, totalCount);
    }

    public async Task<(int TotalActivities, int AdminActivities, int StaffActivities, int FailedActivities)> GetLogsSummaryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildAdminStaffBaseQuery();

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        var totalActivities = await query.CountAsync(cancellationToken);

        var adminActivities = await query.CountAsync(a =>
            a.ActorAccount != null &&
            a.ActorAccount.RoleCode != null &&
            a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Admin,
            cancellationToken);

        var staffActivities = await query.CountAsync(a =>
            a.ActorAccount != null &&
            ((a.ActorAccount.RoleCode != null && a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Staff) ||
             (a.ActorAccount.RoleCode == null && a.ActorAccount.IsInternal == true)),
            cancellationToken);

        var failedActivities = await query.CountAsync(a =>
            (a.Action != null && (
                a.Action.ToUpper().Contains("ERROR") ||
                a.Action.ToUpper().Contains("EXCEPTION") ||
                a.Action.ToUpper().Contains("FAIL") ||
                a.Action.ToUpper().Contains("CRITICAL"))) ||
            (a.NewValue != null && (
                a.NewValue.ToLower().Contains("\"status\":\"failed\"") ||
                a.NewValue.ToLower().Contains("\"status\": \"failed\"") ||
                a.NewValue.ToLower().Contains("\"success\":false") ||
                a.NewValue.ToLower().Contains("\"success\": false"))),
            cancellationToken);

        return (totalActivities, adminActivities, staffActivities, failedActivities);
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetPagedAuditLogsAsync(
        string? search,
        string? action,
        string? entityType,
        Guid? actorId,
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

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var et = entityType.Trim().ToLower();
            query = query.Where(a => a.EntityType != null && a.EntityType.ToLower() == et);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToLower();
            query = query.Where(a => a.Action != null && a.Action.ToLower() == act);
        }

        if (actorId.HasValue && actorId.Value != Guid.Empty)
        {
            query = query.Where(a => a.ActorAccountId == actorId.Value);
        }

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
        var log = await BuildAdminStaffBaseQuery()
            .FirstOrDefaultAsync(a => a.AuditLogId == logId, cancellationToken);

        if (log != null)
        {
            await EnrichLogsWithEntityNamesAsync(new List<AuditLog> { log }, cancellationToken);
        }

        return log;
    }

    private IQueryable<AuditLog> BuildAdminStaffBaseQuery()
    {
        return DbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.ActorAccount)
            .Where(a => a.ActorAccount != null &&
                ((a.ActorAccount.RoleCode != null &&
                  (a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Admin ||
                   a.ActorAccount.RoleCode.ToUpper() == RoleConstants.Staff)) ||
                 (a.ActorAccount.RoleCode == null && a.ActorAccount.IsInternal == true)));
    }

    private async Task EnrichLogsWithEntityNamesAsync(List<AuditLog> items, CancellationToken cancellationToken)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        try
        {
            var refundIds = new HashSet<Guid>();
            var certIds = new HashSet<Guid>();
            var userIds = new HashSet<Guid>();
            var locationIds = new HashSet<Guid>();
            var planIds = new HashSet<Guid>();
            var payoutIds = new HashSet<Guid>();
            var gatewayIds = new HashSet<Guid>();
            var reportIds = new HashSet<Guid>();
            var postIds = new HashSet<Guid>();

            foreach (var log in items)
            {
                if (!Guid.TryParse(log.EntityId, out var eid))
                {
                    continue;
                }

                var et = (log.EntityType ?? string.Empty).Trim();
                if (et.Equals("RefundRequest", StringComparison.OrdinalIgnoreCase))
                    refundIds.Add(eid);
                else if (et.Equals("CoachCertificate", StringComparison.OrdinalIgnoreCase))
                    certIds.Add(eid);
                else if (et.Equals("CoachProfile", StringComparison.OrdinalIgnoreCase) ||
                         et.Equals("User", StringComparison.OrdinalIgnoreCase) ||
                         et.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                    userIds.Add(eid);
                else if (et.Equals("Location", StringComparison.OrdinalIgnoreCase))
                    locationIds.Add(eid);
                else if (et.Equals("CoachSubscriptionPlan", StringComparison.OrdinalIgnoreCase))
                    planIds.Add(eid);
                else if (et.Equals("Payout", StringComparison.OrdinalIgnoreCase))
                    payoutIds.Add(eid);
                else if (et.Equals("PaymentGatewayConfig", StringComparison.OrdinalIgnoreCase))
                    gatewayIds.Add(eid);
                else if (et.Equals("Report", StringComparison.OrdinalIgnoreCase))
                    reportIds.Add(eid);
                else if (et.Equals("Post", StringComparison.OrdinalIgnoreCase))
                    postIds.Add(eid);
            }

            var resolvedLabels = new Dictionary<Guid, string>();

            if (refundIds.Count > 0)
            {
                var refunds = await DbContext.RefundRequests
                    .AsNoTracking()
                    .Where(r => refundIds.Contains(r.RefundRequestId))
                    .Select(r => new
                    {
                        r.RefundRequestId,
                        r.OrderId,
                        BuyerName = r.RequestedByNavigation != null
                            ? (r.RequestedByNavigation.FullName ?? r.RequestedByNavigation.Email)
                            : null,
                        PackageTitle = r.Order != null
                            ? r.Order.OrderDetails
                                .Select(d => d.PackageTitle ?? (d.Package != null ? d.Package.Title : null))
                                .FirstOrDefault()
                            : null
                    })
                    .ToListAsync(cancellationToken);

                foreach (var r in refunds)
                {
                    var buyer = !string.IsNullOrWhiteSpace(r.BuyerName)
                        ? r.BuyerName
                        : (!string.IsNullOrWhiteSpace(r.PackageTitle) ? r.PackageTitle : "Trainee");
                    resolvedLabels[r.RefundRequestId] = buyer;
                }
            }

            if (certIds.Count > 0)
            {
                var certs = await DbContext.CoachCertificates
                    .AsNoTracking()
                    .Where(c => certIds.Contains(c.CertificateId))
                    .Select(c => new
                    {
                        c.CertificateId,
                        c.CertificateName,
                        CoachName = c.Coach != null && c.Coach.Coach != null
                            ? (c.Coach.Coach.FullName ?? c.Coach.Coach.Email)
                            : null
                    })
                    .ToListAsync(cancellationToken);

                foreach (var c in certs)
                {
                    resolvedLabels[c.CertificateId] = !string.IsNullOrWhiteSpace(c.CoachName)
                        ? c.CoachName
                        : (!string.IsNullOrWhiteSpace(c.CertificateName) ? c.CertificateName : "Coach Certificate");
                }
            }

            if (userIds.Count > 0)
            {
                var users = await DbContext.Users
                    .AsNoTracking()
                    .Where(u => userIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName, u.Email })
                    .ToListAsync(cancellationToken);

                foreach (var u in users)
                {
                    var label = !string.IsNullOrWhiteSpace(u.FullName)
                        ? u.FullName
                        : (u.Email ?? "User Account");
                    resolvedLabels[u.UserId] = label;
                }
            }

            if (locationIds.Count > 0)
            {
                var locs = await DbContext.Locations
                    .AsNoTracking()
                    .Where(l => locationIds.Contains(l.LocationId))
                    .Select(l => new { l.LocationId, l.LocationName, l.Address })
                    .ToListAsync(cancellationToken);

                foreach (var l in locs)
                {
                    var name = !string.IsNullOrWhiteSpace(l.LocationName) ? l.LocationName : "Training Location";
                    resolvedLabels[l.LocationId] = name;
                }
            }

            if (planIds.Count > 0)
            {
                var plans = await DbContext.CoachSubscriptionPlans
                    .AsNoTracking()
                    .Where(p => planIds.Contains(p.CoachSubscriptionPlansId))
                    .Select(p => new { p.CoachSubscriptionPlansId, p.Description, p.Amount, p.Currency })
                    .ToListAsync(cancellationToken);

                foreach (var p in plans)
                {
                    var label = !string.IsNullOrWhiteSpace(p.Description)
                        ? p.Description
                        : $"{(p.Amount ?? 0):N0} {p.Currency ?? "VND"}";
                    resolvedLabels[p.CoachSubscriptionPlansId] = label;
                }
            }

            if (payoutIds.Count > 0)
            {
                var payouts = await DbContext.Payouts
                    .AsNoTracking()
                    .Where(p => payoutIds.Contains(p.PayoutId))
                    .Select(p => new
                    {
                        p.PayoutId,
                        p.PayoutMonth,
                        p.PayoutYear,
                        CoachName = p.Coach != null && p.Coach.Coach != null
                            ? (p.Coach.Coach.FullName ?? p.Coach.Coach.Email)
                            : null
                    })
                    .ToListAsync(cancellationToken);

                foreach (var p in payouts)
                {
                    resolvedLabels[p.PayoutId] = !string.IsNullOrWhiteSpace(p.CoachName)
                        ? p.CoachName
                        : $"Cycle {p.PayoutMonth:D2}/{p.PayoutYear}";
                }
            }

            if (gatewayIds.Count > 0)
            {
                var gateways = await DbContext.PaymentGatewayConfigs
                    .AsNoTracking()
                    .Where(g => gatewayIds.Contains(g.GatewayId))
                    .Select(g => new { g.GatewayId, g.GatewayName })
                    .ToListAsync(cancellationToken);

                foreach (var g in gateways)
                {
                    if (!string.IsNullOrWhiteSpace(g.GatewayName))
                    {
                        resolvedLabels[g.GatewayId] = g.GatewayName;
                    }
                }
            }

            if (reportIds.Count > 0)
            {
                var reports = await DbContext.Reports
                    .AsNoTracking()
                    .Where(r => reportIds.Contains(r.ReportId))
                    .Select(r => new
                    {
                        r.ReportId,
                        r.Reason,
                        ReportedUserName = r.ReportedUser != null
                            ? (r.ReportedUser.FullName ?? r.ReportedUser.Email)
                            : null
                    })
                    .ToListAsync(cancellationToken);

                foreach (var r in reports)
                {
                    resolvedLabels[r.ReportId] = !string.IsNullOrWhiteSpace(r.ReportedUserName)
                        ? r.ReportedUserName
                        : (!string.IsNullOrWhiteSpace(r.Reason) ? r.Reason : "Violation Report");
                }
            }

            if (postIds.Count > 0)
            {
                var posts = await DbContext.Posts
                    .AsNoTracking()
                    .Where(p => postIds.Contains(p.Id))
                    .Select(p => new
                    {
                        p.Id,
                        p.Content,
                        AuthorName = p.Author != null ? (p.Author.FullName ?? p.Author.Email) : null
                    })
                    .ToListAsync(cancellationToken);

                foreach (var p in posts)
                {
                    if (!string.IsNullOrWhiteSpace(p.AuthorName))
                    {
                        resolvedLabels[p.Id] = p.AuthorName;
                    }
                    else if (!string.IsNullOrWhiteSpace(p.Content))
                    {
                        resolvedLabels[p.Id] = p.Content.Length > 40 ? p.Content[..40] + "…" : p.Content;
                    }
                }
            }

            if (resolvedLabels.Count == 0)
            {
                return;
            }

            foreach (var log in items)
            {
                if (!Guid.TryParse(log.EntityId, out var eid) ||
                    !resolvedLabels.TryGetValue(eid, out var resolvedName) ||
                    string.IsNullOrWhiteSpace(resolvedName))
                {
                    continue;
                }

                log.NewValue = InjectTargetNameIntoJson(log.NewValue, resolvedName);
            }
        }
        catch
        {
            // Non-fatal: if enrichment fails, fallback to existing payload extraction
        }
    }

    private static string? InjectTargetNameIntoJson(string? json, string resolvedName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.Serialize(new { targetName = resolvedName });
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (dict != null && !dict.ContainsKey("targetName"))
            {
                dict["targetName"] = resolvedName;
                return JsonSerializer.Serialize(dict);
            }
        }
        catch
        {
            // Ignore non-object JSON
        }

        return json;
    }
}
