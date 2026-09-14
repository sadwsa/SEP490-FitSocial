using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> AdminLoginAsync(LoginRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> GoogleCodeLoginAsync(GoogleCodeRequestDto request);
}
