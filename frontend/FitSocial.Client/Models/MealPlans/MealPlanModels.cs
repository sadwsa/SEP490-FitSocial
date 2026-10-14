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
}

public class MealPlanDayModel
{
    public short DayNumber { get; set; }
    public List<MealPlanItemModel> Items { get; set; } = new();
    public int TotalCalories => Items.Sum(i => i.Calories ?? 0);
}

public class MealPlanWeekModel
{
    public short WeekNumber { get; set; }
    public List<MealPlanDayModel> Days { get; set; } = new();
}

public class TraineeMealPlanModel
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
    public List<MealPlanWeekModel> Weeks { get; set; } = new();
    public int TotalItems => Weeks.SelectMany(w => w.Days).SelectMany(d => d.Items).Count();
}
