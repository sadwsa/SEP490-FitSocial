using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Reports;

public class SubmitCoachComplaintRequestDto
{
    [Required(ErrorMessage = "Coach is required.")]
    public Guid CoachId { get; set; }

    /// <summary>
    /// Optional fallback if client sends ReportedUserId instead of CoachId.
    /// </summary>
    public Guid ReportedUserId { get; set; }

    public Guid EffectiveCoachId => CoachId != Guid.Empty ? CoachId : ReportedUserId;

    [Required(ErrorMessage = "Complaint title is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Complaint title must be between 3 and 500 characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional fallback if client sends Reason instead of Title.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    public string EffectiveTitle => !string.IsNullOrWhiteSpace(Title) ? Title : Reason;

    [Required(ErrorMessage = "Complaint content is required.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Complaint content must be between 10 and 500 characters.")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional fallback if client sends Description instead of Content.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public string EffectiveContent => !string.IsNullOrWhiteSpace(Content) ? Content : Description;

    public List<string>? EvidenceUrls { get; set; }
}
