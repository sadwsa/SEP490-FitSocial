using System;

namespace FitSocial.Application.DTOs.Coach;

public class CoachListDto
{
    public Guid CoachId { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? CertificateUrl { get; set; }
    public string? Status { get; set; }
    public string? ApprovalStatus { get; set; }
    public System.Collections.Generic.List<string> Locations { get; set; } = new();
}
