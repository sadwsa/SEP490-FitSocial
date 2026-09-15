using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional role tab selected on the login page ("TRAINEE" or "COACH").
    /// When provided and different from the account role, login is rejected
    /// with the same generic message.
    /// </summary>
    public string? RoleCode { get; set; }
}
