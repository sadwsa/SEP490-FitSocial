using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class TrainingPackage
{
    public Guid PackageId { get; set; }

    public Guid CoachId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public int? DurationDays { get; set; }

    public short? SessionCount { get; set; }

    public short? MinAge { get; set; }

    public string? TargetAudience { get; set; }

    public bool? IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
