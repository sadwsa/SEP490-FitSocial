using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Cart
{
    public Guid CartId { get; set; }

    public Guid UserId { get; set; }

    public Guid PackageId { get; set; }

    public int Quantity { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual TrainingPackage Package { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
