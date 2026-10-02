using System;

namespace FitSocial.Domain.Entities;

public partial class CoachUpgrade
{
    public Guid UpgradeId { get; set; }

    public Guid CoachId { get; set; }

    public Guid CoachSubscriptionPlansId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Guid PriceId
    {
        get => CoachSubscriptionPlansId;
        set => CoachSubscriptionPlansId = value;
    }

    public Guid OrderId { get; set; }

    public string? Status { get; set; }

    public DateTime? EndDay { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual CoachSubscriptionPlan CoachSubscriptionPlan { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
