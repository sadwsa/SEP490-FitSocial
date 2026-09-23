using System.Text.Json.Serialization;

namespace FitSocial.Application.DTOs.Users;

/// <summary>
/// Base profile representation containing common user fields.
/// Internal UserId is intentionally not exposed in the API response.
/// </summary>
[JsonDerivedType(typeof(TraineeProfileDto))]
[JsonDerivedType(typeof(CoachProfileDto))]
public class UserProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Profile representation for Trainees. Contains only common profile fields.
/// </summary>
public class TraineeProfileDto : UserProfileDto
{
}

/// <summary>
/// Profile representation for Coaches. Includes coach-specific biography,
/// years of experience, and verification approval status.
/// </summary>
public class CoachProfileDto : UserProfileDto
{
    public string? Bio { get; set; }
    public int? ExperienceYears { get; set; }
    public string? ApprovalStatus { get; set; }
}
