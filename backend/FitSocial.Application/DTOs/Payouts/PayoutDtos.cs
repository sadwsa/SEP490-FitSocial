using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Payouts;

/// <summary>
/// UC-23.1: one row of the Payout History management list.
/// </summary>
public class PayoutHistoryItemDto
{
    public Guid PayoutId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachEmail { get; set; }
    public int PayoutMonth { get; set; }
    public int PayoutYear { get; set; }
    public string CycleLabel => $"{PayoutMonth:D2}/{PayoutYear}";
    public decimal TotalGrossAmount { get; set; }
    public decimal SystemCommissionAmount { get; set; }
    public decimal RefundAdjustmentTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public string? Status { get; set; }
    public string? TransactionRef { get; set; }
    public string? ProcessedByName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int OrderCount { get; set; }

    /// <summary>TH1: coach currently has unresolved reports.</summary>
    public bool HasBlockingReports { get; set; }
    public int BlockingReportCount { get; set; }

    /// <summary>E1: coach has a usable bank account.</summary>
    public bool HasBankAccount { get; set; }

    public bool CanProcess { get; set; }
    public string? BlockReason { get; set; }
}

public class GeneratePayoutsRequestDto
{
    [Range(1, 12)]
    public int Month { get; set; }

    [Range(2000, 2100)]
    public int Year { get; set; }
}

public class GeneratePayoutsResultDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int SkippedZeroNet { get; set; }
}

public class PayoutItemDto
{
    public Guid PayoutItemId { get; set; }
    public Guid OrderId { get; set; }
    public string? BuyerName { get; set; }
    public string? PackageTitle { get; set; }
    public DateTime? OrderCreatedAt { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal RefundAdjustment { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetAmount { get; set; }
    /// <summary>Monthly vesting label, e.g. "Kỳ 2/4". Empty for refund-only lines.</summary>
    public string VestingInfo { get; set; } = string.Empty;
}

public class PayoutBankAccountDto
{
    public Guid BankId { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? AccountName { get; set; }
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// UC-23.1: payout processing modal data (calculation breakdown + bank + guards).
/// </summary>
public class PayoutDetailDto
{
    public Guid PayoutId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachEmail { get; set; }
    public int PayoutMonth { get; set; }
    public int PayoutYear { get; set; }
    public string CycleLabel => $"{PayoutMonth:D2}/{PayoutYear}";
    public decimal TotalGrossAmount { get; set; }
    public decimal SystemCommissionAmount { get; set; }
    public decimal RefundAdjustmentTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public string? Status { get; set; }
    public string? TransactionRef { get; set; }
    public List<PayoutItemDto> Items { get; set; } = new();
    public PayoutBankAccountDto? BankAccount { get; set; }
    public bool HasBlockingReports { get; set; }
    public int BlockingReportCount { get; set; }
    public bool CanProcess { get; set; }
    public string? BlockReason { get; set; }
}

public class ProcessPayoutResultDto
{
    public Guid PayoutId { get; set; }
    public string? Status { get; set; }
    public string? TransactionRef { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
