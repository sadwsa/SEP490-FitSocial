using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Appeals;

public class SubmitAppealRequestDto
{
    [Required(ErrorMessage = "Report is required.")]
    public Guid ReportId { get; set; }

    [Required(ErrorMessage = "Appeal reason summary is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Reason must be between 3 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Appeal detailed explanation is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Content must be between 10 and 2000 characters.")]
    public string Content { get; set; } = string.Empty;

    public List<string>? EvidenceUrls { get; set; }

    /// <summary>
    /// Temporary token issued during login when account is blocked due to reports.
    /// Optional if user is already authenticated with a valid JWT access token.
    /// </summary>
    public string? AppealToken { get; set; }
}

public class AppealMediaDto
{
    public Guid MediaId { get; set; }
    public Guid AppealId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public string MediaType { get; set; } = "IMAGE";
    public short? SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppealableReportDto
{
    public Guid ReportId { get; set; }
    public string? Type { get; set; }
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public Guid? ReportedPostId { get; set; }
    public string? ReportedPostSnippet { get; set; }
    public Guid? ReportedUserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool HasActiveAppeal { get; set; }
    public string? CurrentAppealStatus { get; set; }
    public Guid? ActiveAppealId { get; set; }
}

public class AppealDetailDto
{
    public Guid AppealId { get; set; }
    public Guid ReportId { get; set; }
    public Guid AppealerId { get; set; }
    public string? AppealerName { get; set; }
    public string? AppealerAvatar { get; set; }
    public string? AppealerEmail { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public AppealableReportDto? Report { get; set; }
    public List<AppealMediaDto> Media { get; set; } = new();
}

public class BlockedAccountAppealSessionDto
{
    public string AppealToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public int WarningCount { get; set; }
}
