using System;

namespace FitSocial.Application.DTOs.Reports;

public class ReportListItemDto
{
    public Guid ReportId { get; set; }

    // Reporter Information
    public Guid? ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string? ReporterEmail { get; set; }
    public string? ReporterAvatarUrl { get; set; }

    // Target Information
    public string TargetType { get; set; } = string.Empty; // "POST" or "USER"
    public Guid? TargetId { get; set; }

    // User Target Details (if TargetType is USER)
    public Guid? ReportedUserId { get; set; }
    public string? ReportedUserName { get; set; }
    public string? ReportedUserEmail { get; set; }
    public string? ReportedUserAvatarUrl { get; set; }

    // Post Target Details (if TargetType is POST)
    public Guid? ReportedPostId { get; set; }
    public string? ReportedPostContent { get; set; }
    public Guid? ReportedPostAuthorId { get; set; }
    public string? ReportedPostAuthorName { get; set; }

    // Report Details
    public string? Reason { get; set; }
    public string? Status { get; set; }

    // Resolution Details
    public Guid? ResolvedBy { get; set; }
    public string? ResolverName { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Timestamp
    public DateTime? CreatedAt { get; set; }
}
