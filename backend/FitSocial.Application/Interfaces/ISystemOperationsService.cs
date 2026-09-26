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
}
