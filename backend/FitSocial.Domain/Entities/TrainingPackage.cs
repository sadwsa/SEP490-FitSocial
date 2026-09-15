using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class TrainingPackage
{
    public Guid PackageId { get; set; }

    public Guid CoachId { get; set; }

    public string? Title { get; set; }

    public decimal? Price { get; set; }

    public int? DurationDays { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? OrderDetailsId { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual OrderDetail? OrderDetails { get; set; }
}
