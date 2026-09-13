using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;

namespace FitSocial.Application.Interfaces;

public interface IPaymentService
{
    Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request);
    Task<ApiResponseDto<AuthResponseDto>> ConfirmCoachActivationAsync(ConfirmCoachActivationRequestDto request);

    /// <summary>
    /// Verify OTP + create a LOCKED coach account, a PENDING order and a PayOS payment link.
    /// The account is unlocked only after the payment is verified.
    /// </summary>
    Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(RegisterCoachRequestDto request, string originUrl);

    /// <summary>
    /// Verify the PayOS payment by order code, then unlock the account and sign the coach in.
    /// </summary>
    Task<ApiResponseDto<AuthResponseDto>> CompleteActivationAsync(long orderCode);

    Task<ApiResponseDto<bool>> CancelActivationAsync(long orderCode);
}
