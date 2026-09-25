using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using FitSocial.Application.DTOs.Locations;

namespace FitSocial.Application.DTOs.Users;

/// <summary>
/// Base response DTO representing another user's public profile.
/// Excludes sensitive authentication credentials, passwords, tokens, and internal security info.
/// </summary>
[JsonDerivedType(typeof(TraineeProfileResponseDto))]
[JsonDerivedType(typeof(CoachProfileResponseDto))]
public class UserProfileResponseDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Public profile representation for Trainees.
/// </summary>
public class TraineeProfileResponseDto : UserProfileResponseDto
{
}

/// <summary>
/// Public profile representation for Coaches.
/// Contains coach biography, experience years, rating, and location.
/// Certificate data and sport/sports information are strictly omitted.
/// </summary>
public class CoachProfileResponseDto : UserProfileResponseDto
{
    public int? ExperienceYears { get; set; }
    public double Rating { get; set; }
    public int TotalReviews { get; set; }
}
