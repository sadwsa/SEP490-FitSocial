using System;
using System.Collections.Generic;

namespace FitSocial.Application.DTOs.MealPlan;

public class MealPlanItemDto
{
    public Guid MealPlanItemId { get; set; }
    public short? WeekNumber { get; set; }
    public short? DayNumber { get; set; }
    public string? MealType { get; set; }
    public string? FoodDescription { get; set; }
    public int? Calories { get; set; }
    public short? SortOrder { get; set; }
}

public class MealPlanDayDto
{
    public short DayNumber { get; set; }
    public List<MealPlanItemDto> Items { get; set; } = new();
    public int TotalCalories => Items.Sum(i => i.Calories ?? 0);
}

public class MealPlanWeekDto
{
    public short WeekNumber { get; set; }
    public List<MealPlanDayDto> Days { get; set; } = new();
}

public class TraineeMealPlanDto
{
    public Guid MealPlanId { get; set; }
    public Guid OrderDetailsId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public string? PackageThumbnailUrl { get; set; }
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public List<MealPlanWeekDto> Weeks { get; set; } = new();

    public int TotalItems => Weeks.SelectMany(w => w.Days).SelectMany(d => d.Items).Count();
}

public class CreateMealPlanDto
{
    public Guid OrderDetailsId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Status { get; set; }
    public List<CreateMealPlanItemDto> Items { get; set; } = new();
}

public class CreateMealPlanItemDto
{
    public short WeekNumber { get; set; }
    public short DayNumber { get; set; }
    public string MealType { get; set; } = string.Empty;
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
    public string MealType { get; set; } = string.Empty;
    public string FoodDescription { get; set; } = string.Empty;
    public int? Calories { get; set; }
    public short? SortOrder { get; set; }
}
