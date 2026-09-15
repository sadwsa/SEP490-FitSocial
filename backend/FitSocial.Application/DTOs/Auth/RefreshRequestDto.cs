using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class RefreshRequestDto
{
    [Required(ErrorMessage = "Missing refresh token")]
    public string RefreshToken { get; set; } = string.Empty;
}
