using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for querying Coach Subscription Plan price change audit logs.
/// </summary>
public class SubscriptionPriceHistoryRepository : ISubscriptionPriceHistoryRepository
{
    private readonly FitSocialDbContext _dbContext;

    public SubscriptionPriceHistoryRepository(FitSocialDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(List<SubscriptionPriceHistoryItem> Items, int TotalCount)> GetPriceHistoryAsync(
        Guid? planId,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var auditQuery = _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityType == "CoachSubscriptionPlan" ||
                        a.EntityType == "CoachSubscriptionPlans" ||
                        a.EntityType == "SubscriptionPlan");

        if (planId.HasValue && planId.Value != Guid.Empty)
        {
            var planIdLower = planId.Value.ToString().ToLowerInvariant();
            var planIdUpper = planId.Value.ToString().ToUpperInvariant();
            auditQuery = auditQuery.Where(a => a.EntityId == planIdLower || a.EntityId == planIdUpper);
        }

        if (fromDate.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            auditQuery = auditQuery.Where(a => a.CreatedAt <= toDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = $"%{keyword.Trim()}%";
            var matchedPlanIds = await _dbContext.CoachSubscriptionPlans
                .AsNoTracking()
                .Where(p => p.Description != null && EF.Functions.ILike(p.Description, kw))
                .Select(p => p.CoachSubscriptionPlansId)
                .ToListAsync(cancellationToken);

            var matchedIdStrs = matchedPlanIds
                .SelectMany(id => new[] { id.ToString().ToLowerInvariant(), id.ToString().ToUpperInvariant() })
                .ToList();

            auditQuery = auditQuery.Where(a => a.EntityId != null && matchedIdStrs.Contains(a.EntityId));
        }

        var totalCount = await auditQuery.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return (new List<SubscriptionPriceHistoryItem>(), 0);
        }

        pageIndex = Math.Max(1, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var pagedLogs = await auditQuery
            .Include(a => a.ActorAccount)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var planGuids = pagedLogs
            .Select(l => Guid.TryParse(l.EntityId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToList();

        var planDict = await _dbContext.CoachSubscriptionPlans
            .AsNoTracking()
            .Where(p => planGuids.Contains(p.CoachSubscriptionPlansId))
            .ToDictionaryAsync(p => p.CoachSubscriptionPlansId, p => p.Description, cancellationToken);

        var resultItems = pagedLogs.Select(log =>
        {
            var parsedGuid = Guid.TryParse(log.EntityId, out var g) ? g : Guid.Empty;
            planDict.TryGetValue(parsedGuid, out var planDesc);

            ParsePricePayload(log.OldValue, out var oldPrice, out var oldNotes);
            ParsePricePayload(log.NewValue, out var newPrice, out var newNotes);

            var notes = !string.IsNullOrWhiteSpace(newNotes)
                ? newNotes
                : (!string.IsNullOrWhiteSpace(oldNotes) ? oldNotes : null);

            return new SubscriptionPriceHistoryItem
            {
                HistoryId = log.AuditLogId,
                PlanId = parsedGuid,
                PlanName = planDesc ?? "Coach Subscription Plan",
                OldPrice = oldPrice,
                NewPrice = newPrice,
                ChangedAt = log.CreatedAt ?? DateTime.UtcNow,
                ChangedByUserId = log.ActorAccountId,
                ChangedByName = log.ActorAccount?.FullName ?? log.ActorAccount?.Email ?? "System Admin",
                Notes = notes
            };
        }).ToList();

        return (resultItems, totalCount);
    }

    private static void ParsePricePayload(string? rawValue, out decimal price, out string? notes)
    {
        price = 0m;
        notes = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var trimmed = rawValue.Trim();

        // Try JSON parsing
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                // Look for price property
                foreach (var propName in new[] { "amount", "price", "newPrice", "oldPrice", "Amount", "Price", "NewPrice", "OldPrice", "value", "Value" })
                {
                    if (root.TryGetProperty(propName, out var priceProp))
                    {
                        if (priceProp.ValueKind == JsonValueKind.Number && priceProp.TryGetDecimal(out var p))
                        {
                            price = p;
                            break;
                        }
                        if (priceProp.ValueKind == JsonValueKind.String && decimal.TryParse(priceProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ps))
                        {
                            price = ps;
                            break;
                        }
                    }
                }

                // Look for notes / reason property
                foreach (var propName in new[] { "notes", "reason", "description", "Notes", "Reason", "Description", "comment", "Comment" })
                {
                    if (root.TryGetProperty(propName, out var notesProp) && notesProp.ValueKind == JsonValueKind.String)
                    {
                        notes = notesProp.GetString();
                        break;
                    }
                }

                return;
            }
            catch
            {
                // Fall back to plain decimal parse
            }
        }

        // Plain decimal parse
        if (decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var directPrice))
        {
            price = directPrice;
        }
    }
}
