using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request);
    Task<ApiResponseDto<bool>> VerifyOtpAsync(VerifyOtpRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request);
    Task<ApiResponseDto<Domain.Entities.User>> RegisterCoachAsync(RegisterCoachRequestDto request, string? ipAddress = null);
    Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> AdminLoginAsync(LoginRequestDto request);
    Task<ApiResponseDto<bool>> LogoutAsync(string? jti, DateTime? expiresAtUtc, string? refreshToken = null);
    Task<ApiResponseDto<AuthResponseDto>> RefreshAsync(RefreshRequestDto request);
    Task<ApiResponseDto<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task<ApiResponseDto<bool>> ResetPasswordAsync(ResetPasswordRequestDto request);
    Task<ApiResponseDto<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> GoogleCodeLoginAsync(GoogleCodeRequestDto request);
}
