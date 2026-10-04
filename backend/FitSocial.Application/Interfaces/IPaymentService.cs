using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.DTOs.Payments;

namespace FitSocial.Application.Interfaces;

public interface IPaymentService
{
    Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request);

    Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(RegisterCoachRequestDto request, string originUrl, string? ipAddress = null);

    Task<ApiResponseDto<AuthResponseDto>> CompleteActivationAsync(long orderCode);

    Task<ApiResponseDto<bool>> CancelActivationAsync(long orderCode);

    // UC-20: Purchase Training Package via PayOS VietQR
    Task<ApiResponseDto<PackagePurchasePreviewDto>> PreparePackagePurchaseAsync(Guid buyerId, Guid packageId);

    Task<ApiResponseDto<PackagePaymentLinkDto>> CreatePackagePaymentLinkAsync(Guid buyerId, Guid packageId, string originUrl);

    Task<ApiResponseDto<OrderDto>> CompletePackagePurchaseAsync(long orderCode);

    Task<ApiResponseDto<bool>> CancelPackagePurchaseAsync(long orderCode, Guid? requesterId = null);
}
