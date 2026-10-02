using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class CoachSubscriptionPlan
{
    public Guid CoachSubscriptionPlansId { get; set; }

    public Guid? PriceId { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? Description { get; set; }

    public int? SubscriptionDuration { get; set; }

    public int? TrainingPackageDuration { get; set; }

    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public virtual Price? Price { get; set; }

    public virtual ICollection<CoachUpgrade> CoachUpgrades { get; set; } = new List<CoachUpgrade>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
