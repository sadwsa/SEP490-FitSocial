using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class SendOtpRequestDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    public string? Purpose { get; set; }
}
