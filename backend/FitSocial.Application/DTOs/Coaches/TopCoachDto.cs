namespace FitSocial.Application.DTOs.Coaches;

/// <summary>
/// DTO representing top coach public information.
/// Does not expose any sensitive credentials, passwords, or tokens.
/// </summary>
public class TopCoachDto
{
    public Guid CoachId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public double Rating { get; set; }
    public int TotalReviews { get; set; }
    public string? Bio { get; set; }
    public int? ExperienceYears { get; set; }
}
