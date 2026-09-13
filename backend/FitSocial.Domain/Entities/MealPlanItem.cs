using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class MealPlanItem
{
    public Guid MealPlanItemId { get; set; }

    public Guid MealPlanId { get; set; }

    public short? WeekNumber { get; set; }

    public short? DayNumber { get; set; }

    public string? MealType { get; set; }

    public string? FoodDescription { get; set; }

    public int? Calories { get; set; }

    public short? SortOrder { get; set; }

    public virtual MealPlan MealPlan { get; set; } = null!;
}
