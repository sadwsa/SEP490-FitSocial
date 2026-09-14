using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Auth;

public class GoogleCodeRequestDto
{
    [Required(ErrorMessage = "Missing Google authorization code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Desired role for newly created accounts only ("TRAINEE" or "COACH").
    /// Google sign-up is Trainee-only; COACH is rejected for new emails.
    /// </summary>
    public string? RoleCode { get; set; }

    /// <summary>
    /// The exact redirect URI used in the authorize request.
    /// "postmessage" for the popup flow, or the app callback URL for the redirect flow.
    /// </summary>
    public string? RedirectUri { get; set; }
}
