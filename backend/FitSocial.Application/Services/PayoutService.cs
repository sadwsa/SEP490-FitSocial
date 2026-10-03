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
        IUnitOfWork unitOfWork)
    {
        _payouts = payouts;
        _users = users;
        _bankAccounts = bankAccounts;
        _reports = reports;
        _encryption = encryption;
        _bankGateway = bankGateway;
        _auditLogs = auditLogs;
        _notifications = notifications;
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
            var banks = await _bankAccounts.ListByCoachIdAsync(p.CoachId, cancellationToken);
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
                HasBlockingReports = reportCount > 0,
                BlockingReportCount = reportCount,
                HasBankAccount = banks.Count > 0
            };

            (dto.CanProcess, dto.BlockReason) = EvaluateProcessable(dto.Status, dto.NetPayoutAmount, dto.HasBankAccount, reportCount);
            result.Add(dto);
        }

        return ApiResponseDto<List<PayoutHistoryItemDto>>.Ok(result, "Payout history retrieved successfully.");
    }

    private static (bool canProcess, string? reason) EvaluateProcessable(string? status, decimal net, bool hasBank, int reportCount)
    {
        if (!string.Equals(status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return (false, null); // processed/failed records show no action
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

                // Rebuild pending snapshot. Clear() first: Deleted instances must
                // leave the tracked collection, otherwise EF change tracking
                // can UPDATE old rows instead of INSERTing the new ones.
                _payouts.RemoveItems(existing.PayoutItems.ToList());
                existing.PayoutItems.Clear();
                foreach (var item in BuildItems(existing.PayoutId, calc))
                {
                    existing.PayoutItems.Add(item);
                }
                ApplyTotals(existing, calc);
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

    private static string BuildVestingInfo(DateTime? orderCreatedAt, int? durationDays, int cycleMonth, int cycleYear, bool isGrossLine)
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
        return $"Kỳ {k + 1}/{n}";
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

    private static List<PayoutItem> BuildItems(Guid payoutId, CycleCalc calc)
    {
        var now = DateTime.UtcNow;
        var items = new List<PayoutItem>();

        foreach (var line in calc.Lines)
        {
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

        var banks = await _bankAccounts.ListByCoachIdAsync(payout.CoachId, cancellationToken);
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
                .Select(i => new PayoutItemDto
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
                    VestingInfo = BuildVestingInfo(
                        i.Order?.CreatedAt,
                        i.Order?.OrderDetails.FirstOrDefault()?.PackageDurationDays,
                        payout.PayoutMonth, payout.PayoutYear,
                        (i.GrossAmount ?? 0) > 0)
                }).ToList()
        };

        (dto.CanProcess, dto.BlockReason) = EvaluateProcessable(
            dto.Status, dto.NetPayoutAmount, dto.BankAccount != null, reportCount);

        return ApiResponseDto<PayoutDetailDto>.Ok(dto, "Payout detail retrieved successfully.");
    }

    // ---------- Process ----------

    public async Task<ApiResponseDto<ProcessPayoutResultDto>> ProcessPayoutAsync(
        Guid payoutId, Guid staffId, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var payout = await _payouts.GetByIdWithDetailsAsync(payoutId, cancellationToken);
        if (payout == null)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Payout record not found.");
        }

        if (!string.Equals(payout.Status, PayoutConstants.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail($"Only pending payouts can be processed (current status: {payout.Status}).");
        }

        var net = payout.NetPayoutAmount ?? 0;
        if (net <= 0)
        {
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Payout has no positive disbursement balance.");
        }

        // 23.1.E1 Missing or Inactive Coach Payment Account
        var banks = await _bankAccounts.ListByCoachIdAsync(payout.CoachId, cancellationToken);
        var bank = banks.FirstOrDefault(b => b.IsDefault == true) ?? banks.FirstOrDefault();
        if (bank == null)
        {
            await WriteAuditAsync(staffId, "PAYOUT_BLOCKED", payout, "PENDING",
                new { reason = "E1: missing bank account" }, ipAddress, cancellationToken);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Coach has not added or activated a valid bank account. Payout rejected and flagged.");
        }

        // TH1: unresolved reports block the transfer
        var reportCount = await _reports.CountUnresolvedAgainstUserAsync(payout.CoachId, cancellationToken);
        if (reportCount > 0)
        {
            await WriteAuditAsync(staffId, "PAYOUT_BLOCKED", payout, "PENDING",
                new { reason = "TH1: unresolved reports", reportCount }, ipAddress, cancellationToken);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(
                $"Coach has {reportCount} unresolved report(s). Payout is blocked until reports are resolved.");
        }

        // Execute transfer via banking gateway
        var accountNumber = _encryption.Decrypt(bank.EncryptedAccountNumber) ?? string.Empty;
        BankTransferResult transfer;
        try
        {
            transfer = await _bankGateway.TransferAsync(new BankTransferRequest(
                bank.BankCode ?? string.Empty,
                accountNumber,
                bank.AccountName ?? string.Empty,
                net,
                "VND",
                $"FITCONNECT PAYOUT {payout.PayoutMonth} {payout.PayoutYear}",
                $"PO{payout.PayoutId:N}"), cancellationToken);
        }
        catch (Exception ex)
        {
            // 23.1.E2 Banking Gateway failure
            await WriteAuditAsync(staffId, "PAYOUT_FAILED", payout, "PENDING",
                new { reason = "E2: gateway exception", error = ex.Message }, ipAddress, cancellationToken);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail("Banking gateway error. Payout kept as PENDING — please retry.");
        }

        if (!transfer.Success)
        {
            await WriteAuditAsync(staffId, "PAYOUT_FAILED", payout, "PENDING",
                new { reason = "E2: transfer rejected", error = transfer.FailureReason }, ipAddress, cancellationToken);
            return ApiResponseDto<ProcessPayoutResultDto>.Fail(
                $"Payout transfer failed ({transfer.FailureReason}). Payout kept as PENDING — please retry.");
        }

        var now = DateTime.UtcNow;
        payout.Status = PayoutConstants.StatusProcessed;
        payout.TransactionRef = transfer.TransactionRef;
        payout.ProcessedBy = staffId;
        payout.ProcessedAt = now;

        await WriteAuditAsync(staffId, "PAYOUT_PROCESSED", payout, "PROCESSED",
            new { transactionRef = transfer.TransactionRef, net, processedAt = now }, ipAddress, cancellationToken);

        try
        {
            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = payout.CoachId,
                ActorId = staffId,
                ReferenceId = payout.PayoutId,
                Type = "PAYOUT_PROCESSED",
                Description = $"Your {payout.PayoutMonth:D2}/{payout.PayoutYear} payout of {net:N0} VND has been processed. Ref: {transfer.TransactionRef}.",
                IsRead = false,
                CreatedAt = now
            }, cancellationToken);
        }
        catch
        {
            // Notifications must never break payout fulfillment
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
                NetPayoutAmount = net,
                ProcessedAt = now
            },
            "Payout processed successfully.");
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
