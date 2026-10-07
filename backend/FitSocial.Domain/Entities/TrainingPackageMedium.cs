using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class TrainingPackageMedium
{
    public Guid MediaId { get; set; }

    public Guid PackageId { get; set; }

    public string MediaUrl { get; set; } = string.Empty;

    public string? MediaType { get; set; } = "IMAGE";

    public short? SortOrder { get; set; } = 0;

    public DateTime? CreatedAt { get; set; }

    public virtual TrainingPackage Package { get; set; } = null!;
}
