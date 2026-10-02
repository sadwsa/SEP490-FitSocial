using System;

namespace FitSocial.Domain.Entities;

public partial class ReportMedium
{
    public Guid MediaId { get; set; }

    public Guid ReportId { get; set; }

    public string? MediaUrl { get; set; }

    public string? MediaType { get; set; }

    public string? MediaFor { get; set; }

    public short? SortOrder { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Report Report { get; set; } = null!;
}
