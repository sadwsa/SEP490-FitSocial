using System;

namespace FitSocial.Client.Models.Users;

public class AdminUserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
}

public class UpdateUserLockStatusRequest
{
    public bool IsLocked { get; set; }
}

public class CreateStaffAccountRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class UpdateStaffAccountRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
    public string? ConfirmPassword { get; set; }
}

public class UpdateOwnProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public int? ExperienceYears { get; set; }
}

/// <summary>
/// Profile model for the currently logged-in user (Trainee or Coach).
/// Does not expose internal UserId or certificate information.
/// </summary>
public class UserProfileModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }

    // Coach-specific fields (null when role is Trainee)
    public string? Bio { get; set; }
    public int? ExperienceYears { get; set; }
    public string? ApprovalStatus { get; set; }

    public bool IsCoach => string.Equals(Role, "COACH", StringComparison.OrdinalIgnoreCase);
    public bool IsTrainee => string.Equals(Role, "TRAINEE", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Public profile model for viewing another user's profile (Trainee or Coach).
/// Strictly excludes sensitive credentials, emails, phone numbers, certificates, and sport data.
/// </summary>
public class OtherUserProfileModel
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

    // Coach-specific properties (null when role is Trainee)
    public int? ExperienceYears { get; set; }
    public double? Rating { get; set; }
    public int? TotalReviews { get; set; }

    public bool IsCoach => string.Equals(Role, "COACH", StringComparison.OrdinalIgnoreCase);
    public bool IsTrainee => string.Equals(Role, "TRAINEE", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Profile model for the currently logged-in Staff or Admin user.
/// Strictly limited to the 7 core fields returned by GET /api/users/me/profile.
/// </summary>
public class StaffAdminOwnProfileDto
{
    public string? Avatar { get; set; }
    public string? FullName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }

    public bool IsAdmin => string.Equals(Role, "ADMIN", StringComparison.OrdinalIgnoreCase);
    public bool IsStaff => string.Equals(Role, "STAFF", StringComparison.OrdinalIgnoreCase);
}

