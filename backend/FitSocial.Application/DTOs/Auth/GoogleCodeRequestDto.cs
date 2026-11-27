using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class GoogleCodeRequestDto
{
    [Required(ErrorMessage = "Missing Google authorization code")]
    public string Code { get; set; } = string.Empty;

    public string? RoleCode { get; set; }

    public string? RedirectUri { get; set; }
}
