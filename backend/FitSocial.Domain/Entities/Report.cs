using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Report
{
    public Guid ReportId { get; set; }

    public Guid ReporterId { get; set; }

    public Guid? ReportedUserId { get; set; }

    public Guid? ReportedPostId { get; set; }

    public string? Type { get; set; }

    public string? Reason { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? AppealStatus { get; set; }

    public string? AppealContent { get; set; }

    public DateTime? AppealedAt { get; set; }

    public Guid? AppealReviewedBy { get; set; }

    public DateTime? AppealReviewedAt { get; set; }

    public string? AppealReviewNote { get; set; }

    public DateTime? NotifiedReportedUserAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Post? ReportedPost { get; set; }

    public virtual User? ReportedUser { get; set; }

    public virtual User Reporter { get; set; } = null!;

    public virtual User? ResolvedByNavigation { get; set; }

    public virtual User? AppealReviewedByNavigation { get; set; }

    public virtual ICollection<ReportMedium> ReportMedia { get; set; } = new List<ReportMedium>();
}
