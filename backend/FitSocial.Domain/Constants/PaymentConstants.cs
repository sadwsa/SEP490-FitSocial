namespace FitSocial.Domain.Constants;

public static class PaymentConstants
{
    public const string CurrencyVnd = "VND";

    public const string OrderTypeCoachActivation = "COACH_ACTIVATION";
    public const string OrderTypePackage = "PACKAGE";
    public const string OrderStatusPending = "PENDING";
    public const string OrderStatusPaid = "PAID";
    // UC-20: package orders are marked Completed on PayOS confirmation
    // (PAID is kept as alias for backward-compat with dashboards/reports).
    public const string OrderStatusCompleted = "COMPLETED";
    public const string OrderStatusCancelled = "CANCELLED";
    public const string OrderStatusFailed = "FAILED";

    public const string PaymentStatusPending = "PENDING";
    public const string PaymentStatusSuccess = "SUCCESS";
    public const string PaymentStatusFailed = "FAILED";
}
