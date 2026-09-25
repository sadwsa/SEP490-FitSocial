using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Report
{
    public Guid ReportId { get; set; }

    public Guid? ReporterId { get; set; }

    public Guid? ReportedUserId { get; set; }

    public Guid? ReportedPostId { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Post? ReportedPost { get; set; }

    public virtual User? ReportedUser { get; set; }

    public virtual User? Reporter { get; set; }

    public virtual User? ResolvedByNavigation { get; set; }
}
