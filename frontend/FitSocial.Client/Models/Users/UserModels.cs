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
