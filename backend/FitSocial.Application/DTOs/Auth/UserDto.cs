namespace FitSocial.Application.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? RoleCode { get; set; }
    public string? AvatarUrl { get; set; }
}
