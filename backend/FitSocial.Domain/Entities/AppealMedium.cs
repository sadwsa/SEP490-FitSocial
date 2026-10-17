using System;

namespace FitSocial.Domain.Entities;

public partial class AppealMedium
{
    public Guid MediaId { get; set; }
    public Guid AppealId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "IMAGE";
    public short? SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Appeal Appeal { get; set; } = null!;
}
