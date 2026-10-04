namespace FitSocial.Client.Models.Payouts;

public class PayoutHistoryItem
{
    public Guid PayoutId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachEmail { get; set; }
    public int PayoutMonth { get; set; }
    public int PayoutYear { get; set; }
    public string CycleLabel { get; set; } = string.Empty;
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
    public bool HasBlockingReports { get; set; }
    public int BlockingReportCount { get; set; }
    public bool HasBankAccount { get; set; }
    public bool CanProcess { get; set; }
    public string? BlockReason { get; set; }
}

public class GeneratePayoutsResult
{
    public int Month { get; set; }
    public int Year { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int SkippedZeroNet { get; set; }
}

public class PayoutItem
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
    public string VestingInfo { get; set; } = string.Empty;
}

public class PayoutBankAccount
{
    public Guid BankId { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? AccountName { get; set; }
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public bool IsDefault { get; set; }
}

public class PayoutDetail
{
    public Guid PayoutId { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? CoachEmail { get; set; }
    public int PayoutMonth { get; set; }
    public int PayoutYear { get; set; }
    public string CycleLabel { get; set; } = string.Empty;
    public decimal TotalGrossAmount { get; set; }
    public decimal SystemCommissionAmount { get; set; }
    public decimal RefundAdjustmentTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public string? Status { get; set; }
    public string? TransactionRef { get; set; }
    public List<PayoutItem> Items { get; set; } = new();
    public PayoutBankAccount? BankAccount { get; set; }
    public bool HasBlockingReports { get; set; }
    public int BlockingReportCount { get; set; }
    public bool CanProcess { get; set; }
    public string? BlockReason { get; set; }
}

public class ProcessPayoutResult
{
    public Guid PayoutId { get; set; }
    public string? Status { get; set; }
    public string? TransactionRef { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
