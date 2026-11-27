using System;
using System.Linq;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Policies;

public sealed record PurchasedPackageEnrollmentState(
    DateTime? ExpiresAt,
    string EnrollmentStatus,
    int? RemainingDays,
    string? RefundStatus,
    string? RefundStaffNote,
    bool IsRefundApproved,
    bool IsRefundPending,
    Guid? TrainingPlanId,
    string? PlanStatus,
    Guid? MealPlanId,
    string? MealPlanStatus);

/// <summary>
/// Shared business policy for resolving purchased package refund, plan, expiry, and enrollment status.
/// </summary>
public static class PurchasedPackageEnrollmentPolicy
{
    public static DateTime? ResolveExpiresAt(OrderDetail od)
    {
        var startDate = od.Order?.CreatedAt ?? od.CreatedAt;
        var durationDays = od.PackageDurationDays ?? od.Package?.DurationDays ?? 30;
        if (!startDate.HasValue || durationDays <= 0)
            return null;

        return DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc).AddDays(durationDays);
    }

    public static PurchasedPackageEnrollmentState Resolve(OrderDetail od, DateTime nowUtc)
    {
        string? refundStatus = null;
        string? refundStaffNote = null;

        var latestRefund = od.Order?.RefundRequests?
            .OrderByDescending(r => r.RequestedAt ?? r.CreatedAt)
            .FirstOrDefault();

        if (latestRefund != null)
        {
            refundStatus = latestRefund.Status;
            if (string.Equals(latestRefund.Status, "REJECTED", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(latestRefund.StaffNote)
                && !latestRefund.StaffNote.TrimStart().StartsWith("[Policy:", StringComparison.OrdinalIgnoreCase))
            {
                refundStaffNote = latestRefund.StaffNote.Trim();
            }
        }
        else if (string.Equals(od.Order?.OrderStatus, "REFUND_PENDING", StringComparison.OrdinalIgnoreCase))
        {
            refundStatus = "PENDING";
        }
        else if (string.Equals(od.Order?.OrderStatus, "REFUNDED", StringComparison.OrdinalIgnoreCase))
        {
            refundStatus = "APPROVED";
        }

        var plan = od.TrainingPlans?.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var mealPlan = od.MealPlans?.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

        var trainingPlanId = plan?.TrainingPlanId;
        var planStatus = plan?.Status;
        var mealPlanId = mealPlan?.MealPlanId;
        var mealPlanStatus = mealPlan?.Status;

        var expiresAt = ResolveExpiresAt(od);
        var orderStatus = od.Order?.OrderStatus;

        var isRefundApproved = string.Equals(refundStatus, "APPROVED", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(orderStatus, "REFUNDED", StringComparison.OrdinalIgnoreCase);
        var isRefundPending = string.Equals(refundStatus, "PENDING", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(orderStatus, "REFUND_PENDING", StringComparison.OrdinalIgnoreCase);

        var pkgType = (od.PackageType ?? od.Package?.PackageType ?? "BOTH").Trim().ToUpperInvariant();
        var planCompleted = string.Equals(planStatus?.Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase);
        var mealCompleted = string.Equals(mealPlanStatus?.Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase);
        var isProgramCompleted = (pkgType == "WORKOUT" && planCompleted)
                              || (pkgType == "MEAL" && mealCompleted)
                              || (pkgType == "BOTH" && planCompleted && (!mealPlanId.HasValue || mealCompleted));

        string enrollmentStatus;
        int? remainingDays;

        if (isRefundApproved)
        {
            enrollmentStatus = "REFUNDED";
            remainingDays = 0;
        }
        else if (isRefundPending)
        {
            enrollmentStatus = "REFUND_PENDING";
            remainingDays = expiresAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiresAt.Value - nowUtc).TotalDays))
                : 0;
        }
        else if (isProgramCompleted)
        {
            enrollmentStatus = "COMPLETED";
            remainingDays = expiresAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiresAt.Value - nowUtc).TotalDays))
                : 0;
        }
        else if (expiresAt.HasValue && nowUtc >= expiresAt.Value)
        {
            enrollmentStatus = "EXPIRED";
            remainingDays = 0;
        }
        else
        {
            enrollmentStatus = "ACTIVE";
            remainingDays = expiresAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((expiresAt.Value - nowUtc).TotalDays))
                : null;
        }

        return new PurchasedPackageEnrollmentState(
            expiresAt,
            enrollmentStatus,
            remainingDays,
            refundStatus,
            refundStaffNote,
            isRefundApproved,
            isRefundPending,
            trainingPlanId,
            planStatus,
            mealPlanId,
            mealPlanStatus);
    }
}
