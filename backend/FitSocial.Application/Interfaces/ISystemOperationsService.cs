using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemOperations;

namespace FitSocial.Application.Interfaces;

public interface ISystemOperationsService
{
    /// <summary>
    /// UC_37: Staff/Admin View Coach Subscription Plans
    /// </summary>
    Task<ApiResponseDto<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(
        bool? isActive = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_37.1: Staff/Admin Create Coach Subscription Plan
    /// </summary>
    Task<ApiResponseDto<CoachSubscriptionPlanDto>> CreatePlanAsync(CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_33.2: Staff/Admin Edit Coach Subscription Plan
    /// </summary>
    Task<ApiResponseDto<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid planId, UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_33.3: Staff/Admin Delete Coach Subscription Plan (hard-delete if unreferenced, else soft-delete).
    /// </summary>
    Task<ApiResponseDto<bool>> DeletePlanAsync(Guid planId, CancellationToken cancellationToken = default);
}
