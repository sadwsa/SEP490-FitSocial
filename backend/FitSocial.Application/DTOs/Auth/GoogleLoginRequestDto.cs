using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class GoogleLoginRequestDto
{
    [Required(ErrorMessage = "Missing Google ID Token")]
    public string IdToken { get; set; } = string.Empty;
}
