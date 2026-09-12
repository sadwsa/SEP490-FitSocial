using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request);
}
