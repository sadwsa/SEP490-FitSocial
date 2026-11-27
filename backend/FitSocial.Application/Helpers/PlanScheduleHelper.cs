using System;
using System.Collections.Generic;
using FitSocial.Application.Exceptions;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;

namespace FitSocial.Application.Helpers;

public static class PlanScheduleHelper
{
    private static readonly TimeSpan BusinessTimeZoneOffset = TimeSpan.FromHours(7);

    private static readonly HashSet<string> AllowedTraineeOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        PaymentConstants.OrderStatusPaid,
        PaymentConstants.OrderStatusActive,
        PaymentConstants.OrderStatusCompleted,
        PaymentConstants.OrderStatusRefundPending
    };

    public static DateTime GetBusinessToday() => DateTime.UtcNow.Add(BusinessTimeZoneOffset).Date;

    public static DateTime GetStartMonday(DateTime startDate)
    {
        int dayOfWeek = (int)startDate.DayOfWeek;
        int daysSinceMonday = (dayOfWeek == 0 ? 7 : dayOfWeek) - 1;
        return startDate.Date.AddDays(-daysSinceMonday);
    }

    public static DateTime GetSessionDate(DateTime startDate, short weekNumber, short dayNumber)
    {
        var startMonday = GetStartMonday(startDate);
        return startMonday.AddDays((weekNumber - 1) * 7 + (dayNumber - 1));
    }

    public static DateTime? ResolveScheduledDate(DateTime? planStartDate, DateTime? planEndDate, short weekNumber, short dayNumber)
    {
        if (!planStartDate.HasValue)
        {
            return null;
        }

        var date = GetSessionDate(planStartDate.Value.Date, weekNumber, dayNumber);

        if (planEndDate.HasValue && date.Date > planEndDate.Value.Date)
        {
            return null;
        }

        return date;
    }

    public static void ValidateOrderStatusForTraineeAccess(Order? order, string planTypeLabel = "plan")
    {
        if (order == null || string.IsNullOrWhiteSpace(order.OrderStatus))
        {
            return;
        }

        var status = order.OrderStatus.Trim();
        if (string.Equals(status, PaymentConstants.OrderStatusRefunded, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException($"This package has been refunded and its {planTypeLabel} is no longer accessible.");
        }

        if (!AllowedTraineeOrderStatuses.Contains(status))
        {
            throw new ForbiddenException($"{char.ToUpperInvariant(planTypeLabel[0])}{planTypeLabel[1..]} is not accessible for order with status '{order.OrderStatus}'.");
        }
    }

    public static double CalculateAdherenceRate(int completedCount, int missedCount, int totalCount)
    {
        if (completedCount + missedCount > 0)
        {
            return Math.Round((double)completedCount / (completedCount + missedCount) * 100, 1);
        }

        return totalCount > 0 ? 100.0 : 0.0;
    }
}
