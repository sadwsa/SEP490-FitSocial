namespace FitSocial.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public UserDto User { get; set; } = new();

    public bool IsBlockedDueToReports { get; set; }

    public string? BlockReasonMessage { get; set; }

    public string? AppealToken { get; set; }

    public int? WarningCount { get; set; }

    public bool IsPaymentPending { get; set; }
}
