namespace FitSocial.Application.Interfaces;

public interface IOtpService
{
    Task<string> GenerateOtpAsync(string email, string purpose);
    Task<(bool Success, string Message)> ValidateOtpAsync(string email, string otpCode, string purpose);
    Task<(bool Success, string Message)> CheckOtpAsync(string email, string otpCode, string purpose);
}
