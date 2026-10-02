using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Reports;

public class ResolveReportRequestDto
{
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "RESOLVED"; // RESOLVED, DISMISSED, REJECTED

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    public bool IssueWarning { get; set; } = true;
}

public class SubmitAppealRequestDto
{
    [Required]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Appeal content must be between 10 and 2000 characters.")]
    public string AppealContent { get; set; } = string.Empty;

    public List<string>? MediaUrls { get; set; }
}

public class ReviewAppealRequestDto
{
    [Required]
    [StringLength(30)]
    public string AppealStatus { get; set; } = "APPROVED"; // APPROVED, REJECTED

    [StringLength(1000)]
    public string? AppealReviewNote { get; set; }
}

public class CreateUserReportRequestDto
{
    [Required]
    public Guid ReportedUserId { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(30)]
    public string? Type { get; set; } = "USER";

    public List<string>? MediaUrls { get; set; }
}

public class ProcessViolationReportRequestDto
{
    [Required(ErrorMessage = "Action is required.")]
    [StringLength(30)]
    public string Action { get; set; } = string.Empty; // WARN, BLOCK, LOCK, DELETE_POST, REJECT, DISMISS

    [StringLength(300, ErrorMessage = "Deletion reason cannot exceed 300 characters.")]
    public string? Reason { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public string? EffectiveReason => !string.IsNullOrWhiteSpace(Reason) ? Reason.Trim() : (!string.IsNullOrWhiteSpace(Note) ? Note.Trim() : null);
}

