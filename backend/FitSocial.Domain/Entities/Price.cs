using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitSocial.Domain.Entities;

public partial class Price
{
    public Guid PriceId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? CreatedAt { get; set; }

    [NotMapped]
    public string? Currency { get; set; }

    [NotMapped]
    public bool? IsActive { get; set; }

    [NotMapped]
    public string? ImageUrl { get; set; }

    [NotMapped]
    public string? Description { get; set; }

    public virtual ICollection<CoachSubscriptionPlan> CoachSubscriptionPlans { get; set; } = new List<CoachSubscriptionPlan>();
}
