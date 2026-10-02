using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class OrderDetail
{
    public Guid OrderDetailsId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? PackageId { get; set; }

    public Guid? CoachSubscriptionPlansId { get; set; }

    public decimal? PackagePrice { get; set; }

    public string? PackageTitle { get; set; }

    public int? PackageDurationDays { get; set; }

    public string? CoachName { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual TrainingPackage? Package { get; set; }

    public virtual CoachSubscriptionPlan? CoachSubscriptionPlan { get; set; }

    public virtual ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();

    public virtual ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
}
