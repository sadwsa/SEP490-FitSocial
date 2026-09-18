namespace FitSocial.Domain.Entities;

/// <summary>
/// Domain model projection representing aggregated coach rating and profile information.
/// </summary>
public class CoachRatingSummary
{
    public Guid CoachId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int? ExperienceYears { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
}
