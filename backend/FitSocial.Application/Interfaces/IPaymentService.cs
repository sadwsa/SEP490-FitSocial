using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;

namespace FitSocial.Application.Interfaces;

public interface IPaymentService
{
    Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request);

    Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(RegisterCoachRequestDto request, string originUrl, string? ipAddress = null);

    Task<ApiResponseDto<AuthResponseDto>> CompleteActivationAsync(long orderCode);

    Task<ApiResponseDto<bool>> CancelActivationAsync(long orderCode);
}
