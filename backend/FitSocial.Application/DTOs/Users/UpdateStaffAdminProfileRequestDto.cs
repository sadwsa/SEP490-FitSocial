using System;

namespace FitSocial.Application.DTOs.Users;

/// <summary>
/// Request DTO for Staff or Admin updating their own profile (UC_35.1).
/// If any field is left empty or null, the existing value in database is retained.
/// </summary>
public class UpdateStaffAdminProfileRequestDto
{
    public string? FullName { get; set; }
    public string? Avatar { get; set; }
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? PhoneNumber { get; set; }
}
