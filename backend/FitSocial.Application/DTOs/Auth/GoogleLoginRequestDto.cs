using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class GoogleLoginRequestDto
{
    [Required(ErrorMessage = "Missing Google ID Token")]
    public string IdToken { get; set; } = string.Empty;

    /// <summary>
    /// Desired role for newly created accounts only ("TRAINEE" or "COACH").
    /// Existing accounts always keep their current role.
    /// </summary>
    public string? RoleCode { get; set; }
}
