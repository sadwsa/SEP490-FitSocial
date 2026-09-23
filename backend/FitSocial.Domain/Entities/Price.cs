using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Price
{
    public Guid PriceId { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public bool? IsActive { get; set; }

    public string? ImageUrl { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CoachUpgrade> CoachUpgrades { get; set; } = new List<CoachUpgrade>();
}
