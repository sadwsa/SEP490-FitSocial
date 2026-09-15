using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class CoachUpgrade
{
    public Guid UpgradeId { get; set; }

    public Guid PriceId { get; set; }

    public Guid CoachId { get; set; }

    public Guid OrderId { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual Price Price { get; set; } = null!;
}
