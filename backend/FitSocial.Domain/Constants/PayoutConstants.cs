namespace FitSocial.Domain.Constants;

/// <summary>
/// UC-23.1 Process Coach Payout.
/// </summary>
public static class PayoutConstants
{
    public const string StatusPending = "PENDING";

    public const string StatusProcessed = "PROCESSED";

    public const string StatusFailed = "FAILED";

    public const decimal CommissionRate = 0m;

    public const decimal TaxRate = 0m;

    public const int MaxVestingMonths = 36;

    public static int VestingMonths(int? durationDays)
    {
        var days = durationDays is > 0 ? durationDays.Value : 30;
        return Math.Min(MaxVestingMonths, Math.Max(1, (int)Math.Ceiling(days / 30.0)));
    }

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

    public static readonly string[] CompletedOrderStatuses = { "PAID", "COMPLETED", "ACTIVE" };

    public static readonly string[] BlockingReportStatuses = { "PENDING", "REVIEWED" };
}
