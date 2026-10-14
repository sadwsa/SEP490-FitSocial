using System.Text.Json;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payouts;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

/// <summary>
/// UC-23.1 Process Coach Payout (Staff/Admin).
/// </summary>
public class PayoutService : IPayoutService
{
    private readonly IPayoutRepository _payouts;
    private readonly IUserRepository _users;
    private readonly ICoachBankAccountRepository _bankAccounts;
    private readonly IReportRepository _reports;
    private readonly IEncryptionService _encryption;
    private readonly IBankTransferGateway _bankGateway;
    private readonly IAuditLogRepository _auditLogs;
    private readonly INotificationRepository _notifications;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Serializes cycle generation: two concurrent Generate clicks must not
    /// insert the same (coach, month, year) snapshot twice (unique index).
    /// </summary>
    private static readonly SemaphoreSlim _generateLock = new(1, 1);

    public PayoutService(
        IPayoutRepository payouts,
        IUserRepository users,
        ICoachBankAccountRepository bankAccounts,
        IReportRepository reports,
        IEncryptionService encryption,
        IBankTransferGateway bankGateway,
        IAuditLogRepository auditLogs,
        INotificationRepository notifications,
        IUnitOfWork unitOfWork,
        IEmailService? emailService = null)
    {
        _payouts = payouts;
        _users = users;
        _bankAccounts = bankAccounts;
        _reports = reports;
        _encryption = encryption;
        _bankGateway = bankGateway;
        _auditLogs = auditLogs;
        _notifications = notifications;
        _emailService = emailService!;
        _unitOfWork = unitOfWork;
    }

    private static (DateTime from, DateTime to) CycleRange(int month, int year)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (from, from.AddMonths(1));
    }

    // ---------- History ----------

    public async Task<ApiResponseDto<List<PayoutHistoryItemDto>>> GetPayoutHistoryAsync(
        int? month, int? year, string? status, Guid? coachId, CancellationToken cancellationToken = default)
    {
        var payouts = await _payouts.ListHistoryAsync(month, year, status?.Trim().ToUpperInvariant(), coachId, cancellationToken);
        var result = new List<PayoutHistoryItemDto>();

        foreach (var p in payouts)
        {
            var reportCount = await _reports.CountUnresolvedAgainstUserAsync(p.CoachId, cancellationToken);
            var banks = await _bankAccounts.ListActiveByCoachIdAsync(p.CoachId, cancellationToken);
            var refundTotal = p.PayoutItems.Sum(i => i.RefundAdjustment ?? 0);

            var dto = new PayoutHistoryItemDto
            {
                PayoutId = p.PayoutId,
                CoachId = p.CoachId,
                CoachName = p.Coach?.Coach?.FullName ?? p.Coach?.Coach?.Email ?? "Coach",
                CoachEmail = p.Coach?.Coach?.Email,
                PayoutMonth = p.PayoutMonth,
                PayoutYear = p.PayoutYear,
                TotalGrossAmount = p.TotalGrossAmount ?? 0,
                SystemCommissionAmount = p.SystemCommissionAmount ?? 0,
                RefundAdjustmentTotal = refundTotal,
                TaxAmount = p.TaxAmount ?? 0,
                NetPayoutAmount = p.NetPayoutAmount ?? 0,
                Status = p.Status,
                TransactionRef = p.TransactionRef,
                ProcessedByName = p.ProcessedByNavigation?.FullName ?? p.ProcessedByNavigation?.Email,
                ProcessedAt = p.ProcessedAt,
                CreatedAt = p.CreatedAt,
                OrderCount = p.PayoutItems.Count(i => (i.GrossAmount ?? 0) > 0),
                PaidItemCount = p.PayoutItems.Count(i => (i.GrossAmount ?? 0) > 0 && IsItemPaid(i, p.Status)),
                TotalItemCount = p.PayoutItems.Count(i => (i.GrossAmount ?? 0) > 0),
                HasBlockingReports = reportCount > 0,
                BlockingReportCount = reportCount,
                HasBankAccount = banks.Count > 0
            };

            (dto.CanProcess, dto.BlockReason) = EvaluateProcessable(
                dto.Status, dto.NetPayoutAmount, dto.HasBankAccount, reportCount,
                dto.PayoutMonth, dto.PayoutYear);
            result.Add(dto);
        }

        return ApiResponseDto<List<PayoutHistoryItemDto>>.Ok(result, "Payout history retrieved successfully.");
    }

    private static bool IsFutureCycle(int month, int year)
    {
        var now = DateTime.UtcNow;
        return year * 12 + month > now.Year * 12 + now.Month;
    }

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    private static string FutureCycleMessage(int month, int year)
    {
        return $"Cannot pay a future cycle yet. Cycle {month:D2}/{year} becomes payable once {MonthNames[month - 1]} {year} arrives.";
    }

    /// <summary>
    /// An item counts as paid when marked PROCESSED, or (legacy rows) when it
    /// belongs to an already-PROCESSED payout.
    /// </summary>
    private static bool IsItemPaid(PayoutItem item, string? payoutStatus)
    {
        if (string.Equals(item.Status, PayoutConstants.StatusProcessed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (item.Status == null &&
            (string.Equals(payoutStatus, PayoutConstants.StatusProcessed, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(payoutStatus, PaymentConstants.OrderStatusPaid, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
    }

    private static (bool canProcess, string? reason) EvaluateProcessable(
        string? status, decimal net, bool hasBank, int reportCount, int month, int year)
    {
        if (!string.Equals(status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null); // processed/failed records show no action
        }
        if (IsFutureCycle(month, year))
        {
            return (false, FutureCycleMessage(month, year));
        }
        if (net <= 0)
        {
            return (false, "Pending payout has no positive disbursement balance.");
        }
        if (!hasBank)
        {
            return (false, "Coach has not added or activated a valid bank account.");
        }
        if (reportCount > 0)
        {
            return (false, $"Coach has {reportCount} unresolved report(s). Resolve reports before payout.");
        }
        return (true, null);
    }

    // ---------- Generate ----------

    public async Task<ApiResponseDto<GeneratePayoutsResultDto>> GenerateMonthlyPayoutsAsync(
        int month, int year, CancellationToken cancellationToken = default)
    {
        if (month < 1 || month > 12)
        {
            return ApiResponseDto<GeneratePayoutsResultDto>.Fail("Invalid month.");
        }

        var now = DateTime.UtcNow;
        if (year < 2000 || year > now.Year + 1)
        {
            return ApiResponseDto<GeneratePayoutsResultDto>.Fail("Invalid year.");
        }

        var (from, to) = CycleRange(month, year);
        await _generateLock.WaitAsync(cancellationToken);
        try
        {
            return await GenerateMonthlyPayoutsCoreAsync(month, year, from, to, now, cancellationToken);
        }
        finally
        {
            _generateLock.Release();
        }
    }

    private async Task<ApiResponseDto<GeneratePayoutsResultDto>> GenerateMonthlyPayoutsCoreAsync(
        int month, int year, DateTime from, DateTime to, DateTime now, CancellationToken cancellationToken)
    {
        // Monthly vesting: a cycle also collects older orders whose revenue
        // vests into this month, so scan back up to MaxVestingMonths.
        var windowFrom = from.AddMonths(-PayoutConstants.MaxVestingMonths);
        var coachIds = await _payouts.ListCoachesWithSalesAsync(windowFrom, to, cancellationToken);

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var coachId in coachIds)
        {
            var calc = await CalculateCycleAsync(coachId, month, year, windowFrom, to, cancellationToken);
            if (calc.Net <= 0)
            {
                skipped++;
                continue;
            }

            var existing = await _payouts.FindCycleAsync(coachId, month, year, cancellationToken);
            if (existing != null)
            {
                if (!string.Equals(existing.Status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // never recalculate a PROCESSED cycle
                }

                // Rebuild pending snapshot but KEEP already-paid lines
                // (per-package payments must survive regeneration).
                var kept = existing.PayoutItems.Where(i => IsItemPaid(i, existing.Status)).ToList();
                var coveredOrders = kept
                    .Where(i => (i.GrossAmount ?? 0) > 0)
                    .Select(i => i.OrderId)
                    .ToHashSet();
                var coveredRefunds = kept
                    .Where(i => (i.RefundAdjustment ?? 0) > 0)
                    .Select(i => i.OrderId)
                    .ToHashSet();
                var doomed = existing.PayoutItems.Where(i => !IsItemPaid(i, existing.Status)).ToList();
                _payouts.RemoveItems(doomed);
                foreach (var dead in doomed)
                {
                    existing.PayoutItems.Remove(dead);
                }
                foreach (var item in BuildItems(existing.PayoutId, calc, coveredOrders, coveredRefunds))
                {
                    existing.PayoutItems.Add(item);
                }
                ApplyItemTotals(existing);
                if ((existing.NetPayoutAmount ?? 0) <= 0 && kept.Count == 0)
                {
                    skipped++;
                    continue;
                }
                updated++;
            }
            else
            {
                var payout = new Payout
                {
                    PayoutId = Guid.NewGuid(),
                    CoachId = coachId,
                    PayoutMonth = month,
                    PayoutYear = year,
                    Status = PayoutConstants.StatusPending,
                    CreatedAt = now
                };
                ApplyTotals(payout, calc);
                foreach (var item in BuildItems(payout.PayoutId, calc))
                {
                    payout.PayoutItems.Add(item);
                }
                await _payouts.AddAsync(payout, cancellationToken);
                created++;
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<GeneratePayoutsResultDto>.Fail("Could not generate payouts. Please try again.");
        }

        return ApiResponseDto<GeneratePayoutsResultDto>.Ok(
            new GeneratePayoutsResultDto { Month = month, Year = year, Created = created, Updated = updated, SkippedZeroNet = skipped },
            $"Payout cycle {month:D2}/{year}: {created} created, {updated} updated.");
    }

    private sealed record VestingLine(Order Order, int TrancheIndex, int TrancheCount, decimal Amount);

    private sealed record CycleCalc(
        List<VestingLine> Lines,
        List<(RefundRequest Refund, decimal Amount)> Refunds,
        decimal Gross,
        decimal Commission,
        decimal RefundTotal,
        decimal Tax,
        decimal Net);

    private static int MonthsBetween(DateTime from, DateTime to)
    {
        return (to.Year - from.Year) * 12 + to.Month - from.Month;
    }

    private static string BuildVestingInfo(
        DateTime? orderCreatedAt, int? durationDays, int cycleMonth, int cycleYear,
        bool isGrossLine, decimal trancheAmount, decimal packageTotal)
    {
        if (!isGrossLine || !orderCreatedAt.HasValue)
        {
            return string.Empty;
        }
        var orderMonth = new DateTime(orderCreatedAt.Value.Year, orderCreatedAt.Value.Month, 1);
        var k = MonthsBetween(orderMonth, new DateTime(cycleYear, cycleMonth, 1));
        var n = PayoutConstants.VestingMonths(durationDays);
        if (k < 0 || k >= n || n <= 1)
        {
            return n <= 1 ? "100% kỳ này" : string.Empty;
        }
        var pct = packageTotal > 0
            ? (trancheAmount / packageTotal * 100m).ToString("0.##")
            : "?";
        return $"Kỳ {k + 1}/{n} • {pct}%";
    }

    private static string PackageGroupKey(Payout payout, Guid orderId)
    {
        var pkg = payout.PayoutItems
            .FirstOrDefault(x => x.OrderId == orderId)?.Order
            ?.OrderDetails.FirstOrDefault()?.PackageId;
        return pkg?.ToString() ?? ("order:" + orderId);
    }

    private static PayoutItemDto MapItemDto(PayoutItem i, Payout payout)
    {
        return new PayoutItemDto
        {
            PayoutItemId = i.PayoutItemId,
            OrderId = i.OrderId,
            BuyerName = i.Order?.Buyer?.FullName ?? i.Order?.Buyer?.Email,
            PackageTitle = i.Order?.OrderDetails.FirstOrDefault()?.PackageTitle,
            OrderCreatedAt = i.Order?.CreatedAt,
            GrossAmount = i.GrossAmount ?? 0,
            CommissionRate = i.CommissionRate ?? PayoutConstants.CommissionRate,
            CommissionAmount = i.CommissionAmount ?? 0,
            RefundAdjustment = i.RefundAdjustment ?? 0,
            TaxAmount = i.TaxAmount ?? 0,
            NetAmount = i.NetAmount ?? 0,
            IsPaid = IsItemPaid(i, payout.Status),
            ProcessedAt = i.ProcessedAt,
            TransactionRef = i.TransactionRef,
            VestingInfo = BuildVestingInfo(
                i.Order?.CreatedAt,
                i.Order?.OrderDetails.FirstOrDefault()?.PackageDurationDays,
                payout.PayoutMonth, payout.PayoutYear,
                (i.GrossAmount ?? 0) > 0,
                i.GrossAmount ?? 0,
                i.Order?.TotalAmount ?? 0)
        };
    }

    /// <summary>
    /// Full month-by-month vesting timeline of one order, marked with what
    /// has already been paid in any cycle. A PAYABLE month carrying an unpaid
    /// item of a still-pending payout can be paid directly (CanPay).
    /// </summary>
    private static List<VestingMonthDto> BuildOrderTimeline(
        Order? order,
        HashSet<(Guid, int, int)> paidCycles,
        Dictionary<(Guid, int, int), (Guid, Guid, bool, bool)> monthLedger,
        bool guardsOk)
    {
        var result = new List<VestingMonthDto>();
        if (order?.CreatedAt == null)
        {
            return result;
        }
        var duration = order.OrderDetails.FirstOrDefault()?.PackageDurationDays;
        var n = PayoutConstants.VestingMonths(duration);
        var gross = order.TotalAmount ?? 0;
        var orderMonth = new DateTime(order.CreatedAt.Value.Year, order.CreatedAt.Value.Month, 1);
        var now = DateTime.UtcNow;
        for (var k = 0; k < n; k++)
        {
            var m = orderMonth.AddMonths(k);
            var paid = paidCycles.Contains((order.OrderId, m.Year, m.Month));
            var arrived = m.Year * 12 + m.Month <= now.Year * 12 + now.Month;
            monthLedger.TryGetValue((order.OrderId, m.Year, m.Month), out var entry);
            var hasUnpaidItem = entry != default && !entry.Item3;
            result.Add(new VestingMonthDto
            {
                Month = m.Month,
                Year = m.Year,
                State = paid ? "PAID" : (arrived ? "PAYABLE" : "FUTURE"),
                Amount = PayoutConstants.TrancheAmount(gross, n, k),
                PayoutId = entry == default ? null : entry.Item1,
                PayoutItemId = entry == default || entry.Item3 ? null : entry.Item2,
                CanPay = guardsOk && arrived && hasUnpaidItem && entry.Item4
            });
        }
        return result;
    }

    private async Task<CycleCalc> CalculateCycleAsync(
        Guid coachId, int month, int year, DateTime windowFrom, DateTime windowTo, CancellationToken ct)
    {
        var orders = await _payouts.ListCompletedOrdersForCycleAsync(coachId, windowFrom, windowTo, ct);
        var cycleStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Monthly vesting: each order contributes 1/N of its price to every
        // month in [purchase month, purchase month + N - 1].
        var lines = new List<VestingLine>();
        foreach (var order in orders)
        {
            if (!order.CreatedAt.HasValue)
            {
                continue;
            }
            var orderMonth = new DateTime(order.CreatedAt.Value.Year, order.CreatedAt.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var k = MonthsBetween(orderMonth, cycleStart);
            var durationDays = order.OrderDetails.FirstOrDefault()?.PackageDurationDays;
            var n = PayoutConstants.VestingMonths(durationDays);
            if (k < 0 || k >= n)
            {
                continue;
            }
            var orderGross = order.TotalAmount ?? 0;
            var amount = PayoutConstants.TrancheAmount(orderGross, n, k);
            if (amount > 0)
            {
                lines.Add(new VestingLine(order, k, n, amount));
            }
        }

        var (from, to) = CycleRange(month, year);
        var refunds = await _payouts.ListApprovedRefundsForCycleAsync(coachId, from, to, ct);

        var vestedIds = lines.Select(l => l.Order.OrderId).ToHashSet();
        // Refunds of orders vesting this cycle are already excluded from gross
        // when the order flips to REFUNDED; only deduct refunds of orders
        // counted in an earlier cycle.
        var priorRefunds = refunds
            .Where(r => !vestedIds.Contains(r.OrderId))
            .Select(r => (Refund: r, Amount: r.ApprovedAmount ?? r.RequestedAmount ?? 0))
            .Where(x => x.Amount > 0)
            .ToList();

        var gross = lines.Sum(l => l.Amount);
        var commission = Math.Round(gross * PayoutConstants.CommissionRate, 2);
        var refundTotal = priorRefunds.Sum(x => x.Amount);
        var taxable = gross - commission - refundTotal;
        var tax = taxable > 0 ? Math.Round(taxable * PayoutConstants.TaxRate, 2) : 0;
        var net = gross - commission - refundTotal - tax;

        return new CycleCalc(lines, priorRefunds, gross, commission, refundTotal, tax, net);
    }

    private static void ApplyTotals(Payout payout, CycleCalc calc)
    {
        payout.TotalGrossAmount = calc.Gross;
        payout.SystemCommissionAmount = calc.Commission;
        payout.TaxAmount = calc.Tax;
        payout.NetPayoutAmount = calc.Net;
    }

    /// <summary>
    /// Recomputes snapshot totals from the actual item set (used when kept
    /// paid lines survive a regeneration).
    /// </summary>
    private static void ApplyItemTotals(Payout payout)
    {
        payout.TotalGrossAmount = payout.PayoutItems.Sum(i => i.GrossAmount ?? 0);
        payout.SystemCommissionAmount = payout.PayoutItems.Sum(i => i.CommissionAmount ?? 0);
        payout.TaxAmount = payout.PayoutItems.Sum(i => i.TaxAmount ?? 0);
        payout.NetPayoutAmount = payout.PayoutItems.Sum(i => i.NetAmount ?? 0);
    }

    private static List<PayoutItem> BuildItems(
        Guid payoutId, CycleCalc calc,
        HashSet<Guid>? skipOrderIds = null, HashSet<Guid>? skipRefundOrderIds = null)
    {
        var now = DateTime.UtcNow;
        var items = new List<PayoutItem>();

        foreach (var line in calc.Lines)
        {
            if (skipOrderIds != null && skipOrderIds.Contains(line.Order.OrderId))
            {
                continue;
            }
            var commission = Math.Round(line.Amount * PayoutConstants.CommissionRate, 2);
            // NOTE: PayoutItemId intentionally left unset (DB default uuid_generate_v4()).
            // These items attach via the tracked Payout collection; an explicitly
            // assigned store-generated key would make EF treat them as EXISTING
            // rows (UPDATE) instead of new rows (INSERT).
            items.Add(new PayoutItem
            {
                PayoutId = payoutId,
                OrderId = line.Order.OrderId,
                GrossAmount = line.Amount,
                CommissionRate = PayoutConstants.CommissionRate,
                CommissionAmount = commission,
                RefundAdjustment = 0,
                TaxAmount = 0,
                NetAmount = line.Amount - commission,
                CreatedAt = now
            });
        }

        foreach (var (refund, amount) in calc.Refunds)
        {
            if (skipRefundOrderIds != null && skipRefundOrderIds.Contains(refund.OrderId))
            {
                continue;
            }
            // Same note as above: leave PayoutItemId for the DB default.
            items.Add(new PayoutItem
            {
                PayoutId = payoutId,
                OrderId = refund.OrderId,
                GrossAmount = 0,
                CommissionRate = PayoutConstants.CommissionRate,
                CommissionAmount = 0,
                RefundAdjustment = amount,
                TaxAmount = 0,
                NetAmount = -amount,
                CreatedAt = now
            });
        }

        return items;
    }

    // ---------- Detail (processing modal) ----------

    public async Task<ApiResponseDto<PayoutDetailDto>> GetPayoutDetailAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        var payout = await _payouts.GetByIdWithDetailsAsync(payoutId, cancellationToken);
        if (payout == null)
        {
            return ApiResponseDto<PayoutDetailDto>.Fail("Payout record not found.");
        }

        var banks = await _bankAccounts.ListActiveByCoachIdAsync(payout.CoachId, cancellationToken);
        var bank = banks.FirstOrDefault(b => b.IsDefault == true) ?? banks.FirstOrDefault();
        var reportCount = await _reports.CountUnresolvedAgainstUserAsync(payout.CoachId, cancellationToken);
        var refundTotal = payout.PayoutItems.Sum(i => i.RefundAdjustment ?? 0);

        var dto = new PayoutDetailDto
        {
            PayoutId = payout.PayoutId,
            CoachId = payout.CoachId,
            CoachName = payout.Coach?.Coach?.FullName ?? payout.Coach?.Coach?.Email ?? "Coach",
            CoachEmail = payout.Coach?.Coach?.Email,
            PayoutMonth = payout.PayoutMonth,
            PayoutYear = payout.PayoutYear,
            TotalGrossAmount = payout.TotalGrossAmount ?? 0,
            SystemCommissionAmount = payout.SystemCommissionAmount ?? 0,
            RefundAdjustmentTotal = refundTotal,
            TaxAmount = payout.TaxAmount ?? 0,
            NetPayoutAmount = payout.NetPayoutAmount ?? 0,
            CommissionRate = payout.PayoutItems.FirstOrDefault(i => (i.GrossAmount ?? 0) > 0)?.CommissionRate
                ?? PayoutConstants.CommissionRate,
            Status = payout.Status,
            TransactionRef = payout.TransactionRef,
            BankAccount = bank == null ? null : new PayoutBankAccountDto
            {
                BankId = bank.BankId,
                BankName = bank.BankName,
                BankCode = bank.BankCode,
                AccountName = bank.AccountName,
                MaskedAccountNumber = MaskAccountNumber(_encryption.Decrypt(bank.EncryptedAccountNumber) ?? string.Empty),
                Branch = bank.Branch,
                IsDefault = bank.IsDefault ?? false
            },
            HasBlockingReports = reportCount > 0,
            BlockingReportCount = reportCount,
            Items = payout.PayoutItems
                .OrderByDescending(i => i.GrossAmount ?? 0)
                .Select(i => MapItemDto(i, payout))
                .ToList()
        };

        // Paid months across ALL cycles of this coach (for per-package timelines).
        // Value: payout + item holding that month's tranche, and whether it is paid.
        var coachPayouts = await _payouts.ListHistoryAsync(null, null, null, payout.CoachId, cancellationToken);
        var monthLedger = new Dictionary<(Guid, int, int), (Guid, Guid, bool, bool)>();
        foreach (var cp in coachPayouts)
        {
            var cpPending = string.Equals(cp.Status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase);
            foreach (var ci in cp.PayoutItems)
            {
                if ((ci.GrossAmount ?? 0) <= 0)
                {
                    continue;
                }
                monthLedger[(ci.OrderId, cp.PayoutYear, cp.PayoutMonth)] =
                    (cp.PayoutId, ci.PayoutItemId, IsItemPaid(ci, cp.Status), cpPending);
            }
        }
        var paidCycles = monthLedger
            .Where(kv => kv.Value.Item3)
            .Select(kv => kv.Key)
            .ToHashSet();

        var guardsOk = bank != null && reportCount == 0;
        foreach (var item in dto.Items.Where(i => i.GrossAmount > 0))
        {
            var order = payout.PayoutItems.FirstOrDefault(x => x.PayoutItemId == item.PayoutItemId)?.Order;
            item.Timeline = BuildOrderTimeline(order, paidCycles, monthLedger, guardsOk);
        }

        dto.PackageGroups = dto.Items
            .Where(i => i.GrossAmount > 0)
            .GroupBy(i => PackageGroupKey(payout, i.OrderId))
            .Select(g =>
            {
                var first = g.First();
                var order = payout.PayoutItems.FirstOrDefault(x => x.PayoutItemId == first.PayoutItemId)?.Order;
                var detail0 = order?.OrderDetails.FirstOrDefault();
                var duration = detail0?.PackageDurationDays ?? 30;
                var paid = g.Where(i => i.IsPaid).Sum(i => i.NetAmount);
                var total = g.Sum(i => i.NetAmount);
                return new PayoutPackageGroupDto
                {
                    PackageId = detail0?.PackageId,
                    PackageTitle = first.PackageTitle ?? "Training Package",
                    DurationDays = duration,
                    MonthsTotal = PayoutConstants.VestingMonths(duration),
                    CycleAmount = total,
                    CyclePaidAmount = paid,
                    CycleUnpaidAmount = total - paid,
                    IsFullyPaid = g.All(i => i.IsPaid),
                    Items = g.ToList()
                };
            })
            .OrderByDescending(g => g.CycleUnpaidAmount)
            .ToList();

        (dto.CanProcess, dto.BlockReason) = EvaluateProcessable(
            dto.Status, dto.NetPayoutAmount, dto.BankAccount != null, reportCount,
            dto.PayoutMonth, dto.PayoutYear);

        return ApiResponseDto<PayoutDetailDto>.Ok(dto, "Payout detail retrieved successfully.");
    }

    // ---------- Process ----------

    private static List<PayoutItem> UnpaidGrossItems(Payout payout)
    {
        return payout.PayoutItems
            .Where(i => (i.GrossAmount ?? 0) > 0 && !IsItemPaid(i, payout.Status))
            .ToList();
    }

    private static List<PayoutItem> PendingRefundItems(Payout payout)
    {
        return payout.PayoutItems
            .Where(i => (i.GrossAmount ?? 0) <= 0 && (i.RefundAdjustment ?? 0) > 0 && !IsItemPaid(i, payout.Status))
            .ToList();
    }

    private sealed record ProcessGuards(CoachBankAccount Bank, string AccountNumber);

    /// <summary>
    /// Shared guards for any payout disbursement (full or per-package):
    /// pending status, arrived cycle, active bank account, no blocking reports.
    /// </summary>
    private async Task<(ProcessGuards? guards, string? error)> CheckProcessGuardsAsync(
        Payout payout, Guid staffId, string? ipAddress, CancellationToken ct)
    {
        if (!string.Equals(payout.Status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return (null, $"Only pending payouts can be processed (current status: {payout.Status}).");
        }

        // Future cycles are not payable yet; overdue (past, still pending) ones are.
        if (IsFutureCycle(payout.PayoutMonth, payout.PayoutYear))
        {
            return (null, FutureCycleMessage(payout.PayoutMonth, payout.PayoutYear));
        }

        // 23.1.E1 Missing or Inactive Coach Payment Account (UC-36.1: inactive excluded)
        var banks = await _bankAccounts.ListActiveByCoachIdAsync(payout.CoachId, ct);
        var bank = banks.FirstOrDefault(b => b.IsDefault == true) ?? banks.FirstOrDefault();
        if (bank == null)
        {
            await WriteAuditAsync(staffId, "PAYOUT_BLOCKED", payout, "PENDING",
                new { reason = "E1: missing bank account" }, ipAddress, ct);
            return (null, "Coach has not added or activated a valid bank account. Payout rejected and flagged.");
        }

        // TH1: unresolved reports block the transfer
        var reportCount = await _reports.CountUnresolvedAgainstUserAsync(payout.CoachId, ct);
        if (reportCount > 0)
        {
            await WriteAuditAsync(staffId, "PAYOUT_BLOCKED", payout, "PENDING",
                new { reason = "TH1: unresolved reports", reportCount }, ipAddress, ct);
            return (null, $"Coach has {reportCount} unresolved report(s). Payout is blocked until reports are resolved.");
        }

        var accountNumber = _encryption.Decrypt(bank.EncryptedAccountNumber) ?? string.Empty;
        return (new ProcessGuards(bank, accountNumber), null);
    }

    private static bool IsOrderRefunded(PayoutItem item)
    {
        return item.Order != null &&
            string.Equals(item.Order.OrderStatus, PaymentConstants.OrderStatusRefunded, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pay-all: transfers the REMAINING balance (unpaid tranches minus
    /// unconsumed refund adjustments) in one transfer.
    /// </summary>
    public async Task<ApiResponseDto<ProcessPayoutResultDto>> ProcessPayoutAsync(
        Guid payoutId, Guid staffId, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var payout = await _payouts.GetByIdWithDetailsAsync(payoutId, cancellationToken);
        if (payout == null)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Payout record not found.");
        }

        var (guards, guardError) = await CheckProcessGuardsAsync(payout, staffId, ipAddress, cancellationToken);
        if (guards == null)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(guardError!);
        }

        var unpaid = UnpaidGrossItems(payout);
        if (unpaid.Count == 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Ok(
                new ProcessPayoutResultDto
                {
                    PayoutId = payout.PayoutId,
                    Status = payout.Status,
                    TransactionRef = payout.TransactionRef,
                    NetPayoutAmount = 0,
                    ProcessedAt = payout.ProcessedAt
                },
                "Payout is already fully paid.");
        }

        // Mutual Consistency: refunded orders require regeneration first.
        if (unpaid.Any(IsOrderRefunded))
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("One or more orders in this payout cycle have been refunded. Please regenerate the payout cycle before processing.");
        }

        var refunds = PendingRefundItems(payout);
        return await ExecuteTransferAsync(
            payout, unpaid, refunds, guards.Bank, guards.AccountNumber,
            staffId, ipAddress, $"PO{payout.PayoutId:N}", cancellationToken);
    }

    /// <summary>
    /// Pay selected lines only (one package group): transfers just their
    /// net amount. The payout stays PENDING until every line is paid.
    /// </summary>
    public async Task<ApiResponseDto<ProcessPayoutResultDto>> ProcessPackageItemsAsync(
        Guid payoutId, List<Guid> payoutItemIds, Guid staffId, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var payout = await _payouts.GetByIdWithDetailsAsync(payoutId, cancellationToken);
        if (payout == null)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Payout record not found.");
        }

        var (guards, guardError) = await CheckProcessGuardsAsync(payout, staffId, ipAddress, cancellationToken);
        if (guards == null)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(guardError!);
        }

        if (payoutItemIds == null || payoutItemIds.Count == 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Select at least one package line to pay.");
        }

        var wanted = payoutItemIds.ToHashSet();
        var targets = payout.PayoutItems
            .Where(i => wanted.Contains(i.PayoutItemId) && (i.GrossAmount ?? 0) > 0 && !IsItemPaid(i, payout.Status))
            .ToList();
        if (targets.Count == 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Selected lines are already paid or do not belong to this payout.");
        }

        if (targets.Any(IsOrderRefunded))
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("One or more selected orders have been refunded. Please regenerate the payout cycle before processing.");
        }

        var orderIds = targets.Select(t => t.OrderId).ToHashSet();
        var refunds = PendingRefundItems(payout).Where(r => orderIds.Contains(r.OrderId)).ToList();

        // Stable idempotency key for this exact selection (safe retries).
        var first = targets.OrderBy(t => t.PayoutItemId).First().PayoutItemId.ToString("N")[..8];
        var reference = $"PO{payout.PayoutId:N}-{first}x{targets.Count}";

        return await ExecuteTransferAsync(
            payout, targets, refunds, guards.Bank, guards.AccountNumber,
            staffId, ipAddress, reference, cancellationToken);
    }

    private async Task<ApiResponseDto<ProcessPayoutResultDto>> ExecuteTransferAsync(
        Payout payout, List<PayoutItem> grossTargets, List<PayoutItem> relatedRefunds,
        CoachBankAccount bank, string accountNumber,
        Guid staffId, string? ipAddress, string reference, CancellationToken ct)
    {
        var amount = grossTargets.Sum(i => i.NetAmount ?? 0) - relatedRefunds.Sum(i => i.RefundAdjustment ?? 0);
        if (amount <= 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Nothing positive to pay after refund adjustments.");
        }

        var now = DateTime.UtcNow;

        // 1. Reconcile first: some targets may already carry a gateway order
        // from an earlier attempt whose response never reached us.
        var withRef = grossTargets.Where(t => !string.IsNullOrWhiteSpace(t.GatewayRef)).ToList();
        foreach (var grp in withRef.GroupBy(t => t.GatewayRef!))
        {
            BankTransferStatus? st = null;
            try
            {
                st = await _bankGateway.GetTransferStatusAsync(grp.Key, ct);
            }
            catch
            {
                st = null;
            }
            if (st != null && st.Found && st.Succeeded)
            {
                foreach (var t in grp)
                {
                    t.Status = PayoutConstants.StatusProcessed;
                    t.ProcessedAt = now;
                    t.TransactionRef = st.TransactionRef ?? st.GatewayRef;
                }
            }
        }

        var remaining = grossTargets.Where(t => !IsItemPaid(t, payout.Status)).ToList();
        if (remaining.Count == 0)
        {
            // Everything was already settled on PayOS by earlier attempts.
            return await FinalizeSettlementAsync(
                payout, 0, null, staffId, ipAddress,
                "Already settled on PayOS by an earlier attempt — records reconciled, no new transfer.", ct);
        }

        var remainingRefunds = relatedRefunds.Where(r => !IsItemPaid(r, payout.Status)).ToList();
        var remainingAmount = remaining.Sum(i => i.NetAmount ?? 0) - remainingRefunds.Sum(i => i.RefundAdjustment ?? 0);
        if (remainingAmount <= 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Nothing positive to pay after refund adjustments.");
        }

        CreatedTransfer created;
        try
        {
            created = await _bankGateway.CreateTransferAsync(new BankTransferRequest(
                bank.BankCode ?? string.Empty,
                accountNumber,
                bank.AccountName ?? string.Empty,
                amount,
                "VND",
                $"FITCONNECT PAYOUT {payout.PayoutMonth} {payout.PayoutYear}",
                reference), ct);
        }
        catch (Exception ex)
        {
            // 23.1.E2 Banking Gateway failure
            await WriteAuditAsync(staffId, "PAYOUT_FAILED", payout, "PENDING",
                new { reason = "E2: gateway exception", error = ex.Message }, ipAddress, ct);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Banking gateway error. Payout kept as PENDING — please retry.");
        }

        if (!created.Created)
        {
            // Create failed (e.g. "idempotency key exists"): the order may
            // already exist on PayOS — look it up by our reference instead of
            // failing blindly, so money is never paid twice nor lost track of.
            BankTransferStatus? found = null;
            try
            {
                found = await _bankGateway.FindTransferByReferenceAsync(reference, ct);
            }
            catch
            {
                found = null;
            }

            if (found != null && found.Found)
            {
                foreach (var t in remaining.Concat(remainingRefunds))
                {
                    t.GatewayRef = found.GatewayRef;
                }
                if (found.Succeeded)
                {
                    foreach (var t in remaining.Concat(remainingRefunds))
                    {
                        t.Status = PayoutConstants.StatusProcessed;
                        t.ProcessedAt = now;
                        t.TransactionRef = found.TransactionRef ?? found.GatewayRef;
                    }
                    return await FinalizeSettlementAsync(
                        payout, remainingAmount, found.TransactionRef ?? found.GatewayRef,
                        staffId, ipAddress,
                        "Found the existing PayOS order and reconciled it — no duplicate transfer.", ct);
                }
                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    return ApiResponseDto<ProcessPayoutResultDto>.Fail("Could not save the payout. Please try again.");
                }
                return ApiResponseDto<ProcessPayoutResultDto>.Fail(
                    $"PayOS already holds this order (state: {found.State ?? "unknown"}). It is recorded for tracking — check again later instead of creating a duplicate. ({created.FailureReason})");
            }

            await WriteAuditAsync(staffId, "PAYOUT_FAILED", payout, "PENDING",
                new { reason = "E2: transfer rejected", error = created.FailureReason }, ipAddress, ct);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(
                $"Payout transfer failed ({created.FailureReason}). Payout kept as PENDING — please retry.");
        }

        // Order accepted — remember its gateway id on every touched line.
        foreach (var t in remaining.Concat(remainingRefunds))
        {
            t.GatewayRef = created.GatewayRef;
        }

        if (!created.Succeeded)
        {
            // Async settlement: PayOS received the order (PROCESSING...).
            // Persist the gateway ref now; staff re-checks via retry.
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return ApiResponseDto<ProcessPayoutResultDto>.Fail("Could not save the payout. Please try again.");
            }
            await WriteAuditAsync(staffId, "PAYOUT_SUBMITTED", payout, "PENDING",
                new { gatewayRef = created.GatewayRef, state = created.State, amount = remainingAmount }, ipAddress, ct);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(
                $"PayOS accepted the order but money has not settled yet (state: {created.State ?? "unknown"}). It is recorded — use retry/check again later instead of creating a duplicate order.");
        }

        foreach (var t in remaining.Concat(remainingRefunds))
        {
            t.Status = PayoutConstants.StatusProcessed;
            t.ProcessedAt = now;
            t.TransactionRef = created.TransactionRef;
        }

        return await FinalizeSettlementAsync(
            payout, remainingAmount, created.TransactionRef, staffId, ipAddress,
            null, ct);
    }

    private async Task<ApiResponseDto<ProcessPayoutResultDto>> FinalizeSettlementAsync(
        Payout payout, decimal amount, string? transactionRef,
        Guid staffId, string? ipAddress, string? customMessage, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fullyPaid = !UnpaidGrossItems(payout).Any();
        if (fullyPaid)
        {
            payout.Status = PayoutConstants.StatusProcessed;
            payout.TransactionRef = transactionRef ?? payout.TransactionRef;
            payout.ProcessedBy = staffId;
            payout.ProcessedAt = now;
        }

        await WriteAuditAsync(staffId, fullyPaid ? "PAYOUT_PROCESSED" : "PAYOUT_PARTIAL", payout,
            fullyPaid ? "PROCESSED" : "PENDING",
            new { transactionRef, amount, processedAt = now }, ipAddress, ct);

        if (fullyPaid)
        {
            try
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = payout.CoachId,
                    ActorId = staffId,
                    ReferenceId = payout.PayoutId,
                    Type = "PAYOUT_PROCESSED",
                    Description = $"Your {payout.PayoutMonth:D2}/{payout.PayoutYear} payout of {amount:N0} VND has been processed. Ref: {transactionRef}.",
                    IsRead = false,
                    CreatedAt = now
                }, ct);
            }
            catch
            {
                // Notifications must never break payout fulfillment
            }
        }

        var paidTargets = payout.PayoutItems
            .Where(i => (i.GrossAmount ?? 0) > 0 && IsItemPaid(i, payout.Status))
            .ToList();
        await SendPayoutEmailAsync(payout, paidTargets, amount, transactionRef, fullyPaid, ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Could not save the payout. Please try again.");
        }

        return ApiResponseDto<ProcessPayoutResultDto>.Ok(
            new ProcessPayoutResultDto
            {
                PayoutId = payout.PayoutId,
                Status = payout.Status,
                TransactionRef = payout.TransactionRef,
                NetPayoutAmount = amount,
                ProcessedAt = now
            },
            customMessage ?? (fullyPaid
                ? "Payout processed successfully."
                : $"Package paid: {amount:N0} VND. Remaining lines stay PENDING."));
    }

    private async Task SendPayoutEmailAsync(
        Payout payout, List<PayoutItem> paidTargets, decimal amount,
        string? transactionRef, bool fullyPaid, CancellationToken ct)
    {
        try
        {
            if (_emailService == null)
            {
                return;
            }

            var coachEmail = payout.Coach?.Coach?.Email;
            if (string.IsNullOrWhiteSpace(coachEmail))
            {
                return;
            }

            var coachName = payout.Coach?.Coach?.FullName ?? coachEmail;
            var groups = paidTargets
                .GroupBy(t => t.Order?.OrderDetails.FirstOrDefault()?.PackageTitle ?? "Training Package")
                .Select(g => new
                {
                    Title = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(t => t.NetAmount ?? 0),
                    Buyers = g.Select(t => t.Order?.Buyer?.FullName ?? t.Order?.Buyer?.Email ?? "Trainee")
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .Take(5)
                        .ToList()
                })
                .ToList();

            var rows = string.Join("", groups.Select(g =>
                "<tr>" +
                $"<td style=\"padding:8px;border:1px solid #e0e0e0;\">{System.Net.WebUtility.HtmlEncode(g.Title)}</td>" +
                $"<td style=\"padding:8px;border:1px solid #e0e0e0;text-align:center;\">{g.Count}</td>" +
                $"<td style=\"padding:8px;border:1px solid #e0e0e0;text-align:right;\">{g.Amount:N0} VND</td>" +
                "</tr>"));

            var subject = fullyPaid
                ? $"[FitConnect] Payout {payout.PayoutMonth:D2}/{payout.PayoutYear} completed: {amount:N0} VND"
                : $"[FitConnect] Partial payout {payout.PayoutMonth:D2}/{payout.PayoutYear}: {amount:N0} VND";
            var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <h2 style=""color: #FF5722; margin: 8px 0 0;"">FitConnect</h2>
                    <p style=""color: #666; margin: 5px 0 0;"">Coach payout notification</p>
                </div>
                <p style=""color: #333; font-size: 16px;"">Hello {System.Net.WebUtility.HtmlEncode(coachName)},</p>
                <p style=""color: #555;"">The system has just paid <strong>{amount:N0} VND</strong> for your {payout.PayoutMonth:D2}/{payout.PayoutYear} earnings{(fullyPaid ? "" : " (partial payment — remaining lines stay pending)")}. Reference: <strong>{System.Net.WebUtility.HtmlEncode(transactionRef ?? "")}</strong>.</p>
                <p style=""color: #555;"">This transfer covers <strong>{groups.Count} package(s)</strong>:</p>
                <table style=""width: 100%; border-collapse: collapse; margin: 12px 0;"">
                    <tr style=""background: #fff3e0;"">
                        <th style=""padding: 8px; border: 1px solid #e0e0e0; text-align: left;"">Package</th>
                        <th style=""padding: 8px; border: 1px solid #e0e0e0;"">Orders</th>
                        <th style=""padding: 8px; border: 1px solid #e0e0e0; text-align: right;"">Amount</th>
                    </tr>
                    {rows}
                    <tr>
                        <td colspan=""2"" style=""padding: 8px; border: 1px solid #e0e0e0; text-align: right;""><strong>Total paid now</strong></td>
                        <td style=""padding: 8px; border: 1px solid #e0e0e0; text-align: right;""><strong>{amount:N0} VND</strong></td>
                    </tr>
                </table>
                <hr style=""border: none; border-top: 1px solid #eee; margin: 20px 0;"" />
                <p style=""color: #999; font-size: 12px; text-align: center; margin: 0;"">If you have questions about this payout, please contact FitConnect support.</p>
            </div>";

            await _emailService.SendEmailAsync(coachEmail, subject, body);
        }
        catch
        {
            // Email must never break payout fulfillment
        }
    }

    private async Task WriteAuditAsync(Guid staffId, string action, Payout payout, string newStatus, object details, string? ipAddress, CancellationToken ct)
    {
        try
        {
            await _auditLogs.AddAsync(new AuditLog
            {
                ActorAccountId = staffId,
                Action = action,
                EntityType = "Payout",
                EntityId = payout.PayoutId.ToString(),
                OldValue = JsonSerializer.Serialize(new { status = payout.Status }),
                NewValue = JsonSerializer.Serialize(new { status = newStatus, details }),
                Ipaddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            }, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            // Audit failures must not break the payout flow itself
        }
    }

    private static string MaskAccountNumber(string number)
    {
        if (string.IsNullOrEmpty(number)) return string.Empty;
        if (number.Length <= 4) return new string('*', number.Length);
        return new string('*', number.Length - 4) + number[^4..];
    }
}
