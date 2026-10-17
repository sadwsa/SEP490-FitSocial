namespace FitSocial.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();

    /// <summary>
    /// Indicates whether the login failed because the account has been blocked due to multiple violation reports.
    /// </summary>
    public bool IsBlockedDueToReports { get; set; }

    public string? BlockReasonMessage { get; set; }

    /// <summary>
    /// Temporary, cryptographically signed token allowing the blocked user to submit an appeal without logging in.
    /// </summary>
    public string? AppealToken { get; set; }

    public int? WarningCount { get; set; }
}
