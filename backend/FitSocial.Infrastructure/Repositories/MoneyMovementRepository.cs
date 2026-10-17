using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Models.Finance;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class MoneyMovementRepository : IMoneyMovementRepository
{
    private readonly FitSocialDbContext _db;

    public MoneyMovementRepository(FitSocialDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Reads every payout line and every refund request, projects both into the same
    /// shape and merges them into one chronologically ordered ledger.
    /// </summary>
    public async Task<List<MoneyMovementRecordModel>> ListAllAsync(CancellationToken ct = default)
    {
        var payoutMovements = await BuildPayoutMovementsAsync(ct);
        var refundMovements = await BuildRefundMovementsAsync(ct);

        return payoutMovements
            .Concat(refundMovements)
            .OrderByDescending(m => m.OccurredOn ?? m.RequestedOn ?? DateTime.MinValue)
            .ThenBy(m => m.MovementType)
            .ToList();
    }

    private async Task<List<MoneyMovementRecordModel>> BuildPayoutMovementsAsync(CancellationToken ct)
    {
        var rows = await _db.PayoutItems
            .AsNoTracking()
            .Include(pi => pi.Payout)
                .ThenInclude(p => p.Coach)
                    .ThenInclude(c => c.Coach)
            .ToListAsync(ct);

        return rows.Select(pi =>
        {
            var coachUser = pi.Payout?.Coach?.Coach;
            var occurredOn = pi.ProcessedAt ?? pi.CreatedAt ?? pi.Payout?.CreatedAt;

            return new MoneyMovementRecordModel
            {
                Id = pi.PayoutItemId,
                MovementType = "PAYOUT",
                RecipientRole = "COACH",
                RecipientId = pi.Payout?.CoachId ?? Guid.Empty,
                RecipientName = coachUser?.FullName,
                RecipientEmail = coachUser?.Email,
                RecipientAvatarUrl = coachUser?.AvatarUrl,
                Amount = pi.NetAmount ?? pi.GrossAmount ?? 0m,
                Status = pi.Status,
                CycleLabel = pi.Payout == null
                    ? null
                    : $"{pi.Payout.PayoutMonth:D2}/{pi.Payout.PayoutYear}",
                OccurredOn = occurredOn,
                RequestedOn = pi.CreatedAt,
                TransactionRef = FirstNonEmpty(pi.TransactionRef, pi.GatewayRef)
            };
        }).ToList();
    }

    private async Task<List<MoneyMovementRecordModel>> BuildRefundMovementsAsync(CancellationToken ct)
    {
        var rows = await _db.RefundRequests
            .AsNoTracking()
            .Include(r => r.RequestedByNavigation)
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var requester = r.RequestedByNavigation;

            return new MoneyMovementRecordModel
            {
                Id = r.RefundRequestId,
                MovementType = "REFUND",
                RecipientRole = "TRAINEE",
                RecipientId = r.RequestedBy,
                RecipientName = requester?.FullName,
                RecipientEmail = requester?.Email,
                RecipientAvatarUrl = requester?.AvatarUrl,
                Amount = r.ApprovedAmount ?? r.RequestedAmount ?? 0m,
                Status = r.Status,
                Note = r.Reason,
                OccurredOn = r.RefundedAt,
                RequestedOn = r.RequestedAt ?? r.CreatedAt,
                TransactionRef = r.RefundTransactionRef
            };
        }).ToList();
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;
}