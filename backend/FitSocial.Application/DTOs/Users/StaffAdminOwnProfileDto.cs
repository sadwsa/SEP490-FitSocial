using System;

namespace FitSocial.Application.DTOs.Users;

/// <summary>
/// Response DTO representing the own profile of an authenticated Staff or Admin user.
/// Strictly limited to the 7 required fields and excludes passwords, internal IDs, and sensitive data.
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
}
