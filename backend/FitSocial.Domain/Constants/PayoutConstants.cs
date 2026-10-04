namespace FitSocial.Domain.Constants;

/// <summary>
/// UC-23.1 Process Coach Payout.
/// </summary>
public static class PayoutConstants
{
    public const string StatusPending = "PENDING";
    public const string StatusProcessed = "PROCESSED";
    public const string StatusFailed = "FAILED";

    /// <summary>
    /// Platform commission taken from each package sale.
    /// Currently 0% per product decision (no platform fee).
    /// </summary>
    public const decimal CommissionRate = 0m;

    /// <summary>
    /// Tax withheld from each payout (assumption: 0 until tax policy is defined).
    /// </summary>
    public const decimal TaxRate = 0m;

    /// <summary>
    /// Upper bound for monthly vesting lookback (3 years of tranches).
    /// </summary>
    public const int MaxVestingMonths = 36;

    /// <summary>
    /// Monthly revenue recognition: a package vests 1/N of its price per
    /// month, where N = number of months of the package duration.
    /// Examples: 4-month package → 25%/month; 2-month → 50%/month;
    /// 1-month (or shorter) → 100% in the purchase month.
    /// </summary>
    public static int VestingMonths(int? durationDays)
    {
        var days = durationDays is > 0 ? durationDays.Value : 30;
        return Math.Min(MaxVestingMonths, Math.Max(1, (int)Math.Ceiling(days / 30.0)));
    }

    /// <summary>
    /// Amount vesting in tranche <paramref name="index"/> (0-based) of
    /// <paramref name="months"/> tranches. The last tranche absorbs rounding.
    /// </summary>
    public static decimal TrancheAmount(decimal gross, int months, int index)
    {
        if (months <= 1 || index < 0)
        {
            return gross;
        }
        if (index >= months - 1)
        {
            var head = Math.Round(gross / months, 2) * (months - 1);
            return gross - head;
        }
        return Math.Round(gross / months, 2);
    }

    /// <summary>
    /// Order statuses counted as completed sales for payout.
    /// </summary>
    public static readonly string[] CompletedOrderStatuses = { "PAID", "COMPLETED", "ACTIVE" };

    /// <summary>
    /// Report statuses that block a payout (unresolved reports against the coach).
    /// </summary>
    public static readonly string[] BlockingReportStatuses = { "PENDING", "REVIEWED" };
}
