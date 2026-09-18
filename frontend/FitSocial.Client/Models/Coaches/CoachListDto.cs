using System;

namespace FitSocial.Client.Models.Coaches;

public class CoachListDto
{
    public Guid CoachId { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Bio { get; set; }
    public string? CertificateUrl { get; set; }
}
