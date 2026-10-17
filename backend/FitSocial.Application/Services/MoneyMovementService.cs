using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Finance;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Models.Finance;

namespace FitSocial.Application.Services;

/// <summary>
/// Read-only ledger of every amount the platform sends out to a coach or a trainee.
/// Filtering and paging happen in memory because the source tables are small
/// (one row per payout line / refund request).
/// </summary>
public class MoneyMovementService : IMoneyMovementService
{
    private readonly IMoneyMovementRepository _repo;

    public MoneyMovementService(IMoneyMovementRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApiResponseDto<MoneyMovementListDto>> GetMoneyMovementsAsync(
        MoneyMovementQueryDto query,
        CancellationToken ct = default)
    {
        query ??= new MoneyMovementQueryDto();

        var all = await _repo.ListAllAsync(ct);
        IEnumerable<MoneyMovementRecordModel> filtered = all;

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            filtered = filtered.Where(m => EffectiveDate(m) >= from);
        }

        if (query.ToDate.HasValue)
        {
            var to = query.ToDate.Value.Date;
            filtered = filtered.Where(m => EffectiveDate(m) <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.MovementType))
        {
            var type = query.MovementType.Trim().ToUpperInvariant();
            filtered = filtered.Where(m => m.MovementType == type);
        }

        if (!string.IsNullOrWhiteSpace(query.RecipientRole))
        {
            var role = query.RecipientRole.Trim().ToUpperInvariant();
            filtered = filtered.Where(m => m.RecipientRole == role);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            filtered = filtered.Where(m =>
                string.Equals(m.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            filtered = filtered.Where(m =>
                (m.RecipientName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.RecipientEmail?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.TransactionRef?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = filtered.ToList();

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var pageNumber = Math.Max(1, query.PageNumber);
        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        var result = new MoneyMovementListDto
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = list.Count,
            TotalPayoutAmount = list.Where(m => m.MovementType == "PAYOUT").Sum(m => m.Amount),
            TotalRefundAmount = list.Where(m => m.MovementType == "REFUND").Sum(m => m.Amount)
        };

        return ApiResponseDto<MoneyMovementListDto>.Ok(result);
    }

    private static MoneyMovementDto ToDto(MoneyMovementRecordModel r) => new()
    {
        Id = r.Id,
        MovementType = r.MovementType,
        RecipientRole = r.RecipientRole,
        RecipientId = r.RecipientId,
        RecipientName = r.RecipientName,
        RecipientEmail = r.RecipientEmail,
        RecipientAvatarUrl = r.RecipientAvatarUrl,
        Amount = r.Amount,
        Status = r.Status,
        CycleLabel = r.CycleLabel,
        Note = r.Note,
        OccurredOn = r.OccurredOn,
        RequestedOn = r.RequestedOn,
        TransactionRef = r.TransactionRef
    };

    private static DateTime EffectiveDate(MoneyMovementRecordModel m) =>
        (m.OccurredOn ?? m.RequestedOn ?? DateTime.MinValue).Date;
}