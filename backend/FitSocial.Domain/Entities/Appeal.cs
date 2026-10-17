using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Appeal
{
    public Guid AppealId { get; set; }
    public Guid ReportId { get; set; }
    public Guid AppealerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public virtual Report Report { get; set; } = null!;
    public virtual User Appealer { get; set; } = null!;
    public virtual User? ReviewedByNavigation { get; set; }
    public virtual ICollection<AppealMedium> AppealMedia { get; set; } = new List<AppealMedium>();
}
