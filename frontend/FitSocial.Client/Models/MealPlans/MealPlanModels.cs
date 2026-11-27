using System;
using System.Collections.Generic;
using System.Linq;

namespace FitSocial.Client.Models.MealPlans;

public class MealPlanItemModel
{
    public Guid MealPlanItemId { get; set; }
    public short? WeekNumber { get; set; }
    public short? DayNumber { get; set; }
    public string? MealType { get; set; }
    public string? FoodDescription { get; set; }
    public int? Calories { get; set; }
    public short? SortOrder { get; set; }

    public bool IsCompleted { get; set; }
    public Guid? MealCompletionLogId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TraineeNotes { get; set; }
    public string? MediaUrl { get; set; }
    public string? CoachFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Status { get; set; }
    public DateTime? ScheduledDate { get; set; }
}

public class MealPlanDayModel
{
    public short DayNumber { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public bool IsToday { get; set; }
    public List<MealPlanItemModel> Items { get; set; } = new();
    public int TotalCalories => Items.Sum(i => i.Calories ?? 0);
    public int TotalMeals => Items.Count;
    public int CompletedMeals => Items.Count(i => i.IsCompleted);
    public int MissedMeals => Items.Count(i => string.Equals(i.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase));
    public bool IsAllCompleted => TotalMeals > 0 && CompletedMeals >= TotalMeals;
}

public class MealPlanWeekModel
{
    public short WeekNumber { get; set; }
    public List<MealPlanDayModel> Days { get; set; } = new();
    public int TotalMeals => Days.Sum(d => d.TotalMeals);
    public int CompletedMeals => Days.Sum(d => d.CompletedMeals);
    public int MissedMeals => Days.Sum(d => d.MissedMeals);
}

public class TraineeMealPlanModel
{
    public Guid MealPlanId { get; set; }
    public Guid OrderDetailsId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public string? PackageThumbnailUrl { get; set; }
    public string PackageType { get; set; } = "BOTH";
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? RemainingDays => EndDate.HasValue
        ? Math.Max(0, (int)Math.Ceiling((EndDate.Value.Date - DateTime.UtcNow.AddHours(7).Date).TotalDays))
        : null;
    public int TotalMealsCount { get; set; }
    public int CompletedMealsCount { get; set; }
    public int MissedMealsCount { get; set; }
    public double AdherenceRate { get; set; }
    public double ProgressPercentage => TotalMealsCount > 0
        ? Math.Round((double)CompletedMealsCount / TotalMealsCount * 100, 1)
        : 0;
    public List<MealPlanWeekModel> Weeks { get; set; } = new();
    public int TotalItems => Weeks.SelectMany(w => w.Days).SelectMany(d => d.Items).Count();
}

public class CreateMealPlanDto
{
    public Guid OrderDetailsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; } = "ACTIVE";
    public List<CreateMealPlanItemDto> Items { get; set; } = new();
}

public class CreateMealPlanItemDto
{
    public Guid? MealPlanItemId { get; set; }
    public short WeekNumber { get; set; }
    public short DayNumber { get; set; }
    public string MealType { get; set; } = "Breakfast";
    public string FoodDescription { get; set; } = string.Empty;
    public int? Calories { get; set; }
    public short? SortOrder { get; set; }
}

public class UpdateMealPlanDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
    public List<UpdateMealPlanItemDto>? Items { get; set; }
}

public class UpdateMealPlanItemDto
{
    public Guid? MealPlanItemId { get; set; }
    public short WeekNumber { get; set; }
    public short DayNumber { get; set; }
    public string MealType { get; set; } = "Breakfast";
    public string FoodDescription { get; set; } = string.Empty;
    public int? Calories { get; set; }
    public short? SortOrder { get; set; }
}

public class RequestMealCompletionDto
{
    public Guid MealPlanItemId { get; set; }
    public DateTime? LogDate { get; set; }
    public string? Notes { get; set; }
    public string? MediaUrl { get; set; }
}

public class SaveMealMissedReasonDto
{
    public Guid MealPlanItemId { get; set; }
    public string? Notes { get; set; }
}

public class CoachReviewMealDto
{
    public Guid MealPlanItemId { get; set; }
    public Guid TraineeId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Feedback { get; set; }
}

public class MealCompletionResultDto
{
    public Guid MealPlanItemId { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? MealCompletionLogId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? MediaUrl { get; set; }
    public string? CoachFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int TotalMealsCount { get; set; }
    public int CompletedMealsCount { get; set; }
    public double ProgressPercentage { get; set; }
}

public class CoachTraineeMealProgressDto
{
    public Guid OrderDetailsId { get; set; }
    public Guid TraineeId { get; set; }
    public string TraineeName { get; set; } = string.Empty;
    public string TraineeEmail { get; set; } = string.Empty;
    public string? TraineeAvatarUrl { get; set; }
    public Guid PackageId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public Guid MealPlanId { get; set; }
    public string? PlanTitle { get; set; }
    public string? PlanStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? RemainingDays => EndDate.HasValue
        ? Math.Max(0, (int)Math.Ceiling((EndDate.Value.Date - DateTime.UtcNow.AddHours(7).Date).TotalDays))
        : null;
    public int TotalMealsCount { get; set; }
    public int CompletedMealsCount { get; set; }
    public int MissedMealsCount { get; set; }
    public double AdherenceRate { get; set; }
    public double ProgressPercentage => TotalMealsCount > 0
        ? Math.Round((double)CompletedMealsCount / TotalMealsCount * 100, 1)
        : 0;
    public List<MealPlanWeekModel> Weeks { get; set; } = new();
    public List<CoachMealActivityLogDto> RecentLogs { get; set; } = new();
}

public class CoachMealActivityLogDto
{
    public Guid LogId { get; set; }
    public Guid MealPlanItemId { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string FoodDescription { get; set; } = string.Empty;
    public short? WeekNumber { get; set; }
    public short? DayNumber { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? MediaUrl { get; set; }
    public string? CoachFeedback { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
