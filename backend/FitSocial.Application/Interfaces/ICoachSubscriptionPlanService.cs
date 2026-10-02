using FitSocial.Application.DTOs.CoachSubscriptionPlans;
using FitSocial.Application.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface ICoachSubscriptionPlanService
{
    Task<ApiResponseDto<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<ApiResponseDto<List<CoachSubscriptionPlanDto>>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CoachSubscriptionPlanDto>> CreatePlanAsync(CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default);
}
