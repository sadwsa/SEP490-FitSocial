using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Price
{
    public Guid PriceId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CoachSubscriptionPlan> CoachSubscriptionPlans { get; set; } = new List<CoachSubscriptionPlan>();
}
