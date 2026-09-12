using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class SendOtpRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    public string? Purpose { get; set; }
}
