using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitSocial.API.Services;

/// <summary>
/// Background service that runs daily at 08:00 AM (Vietnam Time, UTC+7) to monitor active coaching plans.
/// Sends automated SYSTEM notifications (ActorId = null) to coaches:
/// 1. Meal Plan: Warns coach if tomorrow or day T+2 (2 days ahead) has no meals scheduled.
/// 2. Workout Plan: Warns coach if upcoming workout buffer is critically low or empty.
///    - Distinguishes Yellow (2-3 days warning) vs Red (urgent / empty / within 24 hours).
///    - Evaluates remaining sessions (for fixed session packages) or remaining days (for flexible packages where session is null).
///    - Skips alerting once 100% of sessions have been scheduled.
/// </summary>
public class CoachScheduleReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CoachScheduleReminderBackgroundService> _logger;

    public CoachScheduleReminderBackgroundService(
        IServiceProvider services,
        ILogger<CoachScheduleReminderBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial warmup delay 1 minute after server starts up
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        try
        {
            await CheckSchedulesAndNotifyAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial CoachScheduleReminder startup check failed.");
        }

        // Then repeat daily at exactly 08:00 AM Vietnam Time (UTC+7)
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowVn = DateTime.UtcNow.AddHours(7);
            var next8AmVn = nowVn.Date.AddHours(8); // Today 08:00 AM VN
            if (nowVn >= next8AmVn)
            {
                next8AmVn = next8AmVn.AddDays(1); // Tomorrow 08:00 AM VN
            }

            var delayUntilNext8Am = next8AmVn - nowVn;
            _logger.LogInformation("CoachScheduleReminder next daily run at {NextRun:yyyy-MM-dd HH:mm:ss} VN time (in {Hours:F1} hours)", next8AmVn, delayUntilNext8Am.TotalHours);

            await Task.Delay(delayUntilNext8Am, stoppingToken);

            try
            {
                await CheckSchedulesAndNotifyAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily 08:00 AM CoachScheduleReminder execution failed.");
            }
        }
    }

    private async Task CheckSchedulesAndNotifyAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FitSocialDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationRealtimeNotifier>();

        var today = DateTime.UtcNow.AddHours(7).Date;
        var tomorrow = today.AddDays(1);
        var twoDaysAhead = today.AddDays(2);

        await CheckMealPlansAsync(db, notifier, today, tomorrow, twoDaysAhead, ct);
        await CheckWorkoutPlansAsync(db, notifier, today, tomorrow, twoDaysAhead, ct);
    }

    private async Task CheckMealPlansAsync(
        FitSocialDbContext db,
        INotificationRealtimeNotifier notifier,
        DateTime today,
        DateTime tomorrow,
        DateTime twoDaysAhead,
        CancellationToken ct)
    {
        var activeMealPlans = await db.MealPlans
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Order)
                    .ThenInclude(o => o!.Buyer)
            .Include(p => p.MealPlanItems)
            .Where(p => p.Status == "ACTIVE" &&
                        p.StartDate.HasValue &&
                        p.EndDate.HasValue &&
                        p.EndDate.Value.Date >= today)
            .ToListAsync(ct);

        foreach (var plan in activeMealPlans)
        {
            var startDate = plan.StartDate!.Value.Date;
            var endDate = plan.EndDate!.Value.Date;

            // Check day T+2 (2 days ahead)
            if (twoDaysAhead >= startDate && twoDaysAhead <= endDate)
            {
                bool hasMealTwoDaysAhead = plan.MealPlanItems.Any(i =>
                    !string.IsNullOrWhiteSpace(i.FoodDescription) &&
                    MealPlanService.GetSessionDate(startDate, i.WeekNumber ?? 1, i.DayNumber ?? 1).Date == twoDaysAhead);

                if (!hasMealTwoDaysAhead)
                {
                    bool alreadyNotifiedToday = await db.Notifications.AnyAsync(n =>
                        n.UserId == plan.CoachId &&
                        n.ReferenceId == plan.MealPlanId &&
                        n.Type == "MEAL_SCHEDULE_REMINDER" &&
                        n.CreatedAt.HasValue && n.CreatedAt.Value.Date == today, ct);

                    if (!alreadyNotifiedToday)
                    {
                        var traineeName = plan.OrderDetails?.Order?.Buyer?.FullName ?? "Trainee";

                        var notif = new Notification
                        {
                            Id = Guid.NewGuid(),
                            UserId = plan.CoachId,
                            ActorId = null, // Sent by SYSTEM automatically, not by trainee
                            ReferenceId = plan.MealPlanId,
                            Type = "MEAL_SCHEDULE_REMINDER",
                            Description = $"[FitConnect System] Meal plan for trainee {traineeName} on {twoDaysAhead:dd/MM/yyyy} has not been prepared. Please schedule meals at least 2 days in advance so the trainee can prepare.",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        db.Notifications.Add(notif);
                        await db.SaveChangesAsync(ct);

                        try
                        {
                            await notifier.SendNotificationAsync(plan.CoachId, "FitConnect System: Meal Plan Reminder", notif.Description);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send realtime notification to coach {CoachId}", plan.CoachId);
                        }
                    }
                }
            }
        }
    }

    private async Task CheckWorkoutPlansAsync(
        FitSocialDbContext db,
        INotificationRealtimeNotifier notifier,
        DateTime today,
        DateTime tomorrow,
        DateTime twoDaysAhead,
        CancellationToken ct)
    {
        var activeTrainingPlans = await db.TrainingPlans
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Order)
                    .ThenInclude(o => o!.Buyer)
            .Include(p => p.OrderDetails)
                .ThenInclude(od => od!.Package)
            .Include(p => p.TrainingPlanExercises)
            .Where(p => p.Status == "ACTIVE" &&
                        p.StartDate.HasValue &&
                        p.EndDate.HasValue &&
                        p.EndDate.Value.Date >= today)
            .ToListAsync(ct);

        foreach (var plan in activeTrainingPlans)
        {
            var startDate = plan.StartDate!.Value.Date;
            var endDate = plan.EndDate!.Value.Date;

            // Only check plans starting within 3 days or already started
            if (startDate > today.AddDays(3))
            {
                continue;
            }

            var scheduledDates = plan.TrainingPlanExercises
                .Where(e => !string.IsNullOrWhiteSpace(e.ExerciseName))
                .Select(e => TrainingPlanService.GetSessionDate(startDate, e.WeekNumber ?? 1, e.DayNumber ?? 1).Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var maxSessions = plan.OrderDetails?.Package?.SessionCount;
            int? remainingSessions = null;

            if (maxSessions.HasValue && maxSessions.Value > 0)
            {
                remainingSessions = maxSessions.Value - scheduledDates.Count;
                // If coach has scheduled all sessions required for this package, do not alert
                if (remainingSessions <= 0)
                {
                    continue;
                }
            }

            int remainingDays = (endDate - today).Days;
            if (remainingDays < 0)
            {
                continue;
            }

            // Check upcoming sessions scheduled from today onwards
            var upcomingSessions = scheduledDates.Where(d => d >= today).ToList();

            bool needsReminder = false;
            string severity = "WARNING"; // WARNING (Yellow) or URGENT (Red)
            string reminderMessage = string.Empty;
            var traineeName = plan.OrderDetails?.Order?.Buyer?.FullName ?? "Trainee";

            string packageRemainingInfo = remainingSessions.HasValue
                ? $"({remainingSessions.Value} sessions remaining)"
                : $"({remainingDays} days remaining in package)";

            if (upcomingSessions.Count == 0)
            {
                // RED ALERT: Zero upcoming sessions scheduled while plan is active
                needsReminder = true;
                severity = "URGENT";
                reminderMessage = $"[FitConnect System] Trainee {traineeName} currently has no upcoming workout sessions scheduled {packageRemainingInfo}. Please update the workout schedule soon to keep training on track!";
            }
            else if (upcomingSessions.Count == 1 && endDate > upcomingSessions[0])
            {
                var nextSession = upcomingSessions[0];
                int daysUntilSession = (nextSession - today).Days;

                if (daysUntilSession <= 1)
                {
                    // RED ALERT: Only 1 session left, happening today or tomorrow, nothing after
                    needsReminder = true;
                    severity = "URGENT";
                    reminderMessage = $"[FitConnect System] Trainee {traineeName} only has 1 workout session scheduled on {nextSession:dd/MM/yyyy} and no further sessions {packageRemainingInfo}. Please schedule upcoming sessions promptly!";
                }
                else if (daysUntilSession <= 3)
                {
                    // YELLOW ALERT: 1 session left within 2-3 days, buffer is running low
                    needsReminder = true;
                    severity = "WARNING";
                    reminderMessage = $"[FitConnect System] Trainee {traineeName} has their next workout session on {nextSession:dd/MM/yyyy} {packageRemainingInfo}. No further sessions are scheduled after this date; please schedule at least 2 days in advance.";
                }
            }

            if (needsReminder)
            {
                string notifType = severity == "URGENT" ? "WORKOUT_SCHEDULE_URGENT" : "WORKOUT_SCHEDULE_REMINDER";

                bool alreadyNotifiedToday = await db.Notifications.AnyAsync(n =>
                    n.UserId == plan.CoachId &&
                    n.ReferenceId == plan.TrainingPlanId &&
                    (n.Type == "WORKOUT_SCHEDULE_URGENT" || n.Type == "WORKOUT_SCHEDULE_REMINDER") &&
                    n.CreatedAt.HasValue && n.CreatedAt.Value.Date == today, ct);

                if (!alreadyNotifiedToday)
                {
                    var notif = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = plan.CoachId,
                        ActorId = null, // Sent by SYSTEM automatically, not by trainee
                        ReferenceId = plan.TrainingPlanId,
                        Type = notifType,
                        Description = reminderMessage,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Notifications.Add(notif);
                    await db.SaveChangesAsync(ct);

                    try
                    {
                        string title = severity == "URGENT" ? "FitConnect System: Urgent Workout Schedule Alert" : "FitConnect System: Workout Schedule Reminder";
                        await notifier.SendNotificationAsync(plan.CoachId, title, notif.Description);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send realtime notification to coach {CoachId}", plan.CoachId);
                    }
                }
            }
        }
    }
}
