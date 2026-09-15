using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class OrderDetail
{
    public Guid OrderDetailsId { get; set; }

    public Guid OrderId { get; set; }

    public decimal? PackagePrice { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<TrainingPackage> TrainingPackages { get; set; } = new List<TrainingPackage>();

    public virtual ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
}
