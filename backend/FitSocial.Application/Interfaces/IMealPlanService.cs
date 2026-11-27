using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;

namespace FitSocial.Application.Interfaces;

public interface IMealPlanService
{
    Task<TraineeMealPlanDto?> GetTraineeMealPlanAsync(Guid traineeId, Guid orderDetailsId, CancellationToken ct = default);
    Task<TraineeMealPlanDto> CreateMealPlanAsync(Guid coachId, CreateMealPlanDto dto, CancellationToken ct = default);
    Task<TraineeMealPlanDto> UpdateMealPlanAsync(Guid coachId, Guid planId, UpdateMealPlanDto dto, CancellationToken ct = default);
    Task<TraineeMealPlanDto?> GetCoachMealPlanByOrderDetailsAsync(Guid coachId, Guid orderDetailsId, CancellationToken ct = default);

    // Meal Completion Proof & Coach Review
    Task<MealCompletionResultDto> RequestMealCompletionAsync(Guid traineeId, RequestMealCompletionDto dto, CancellationToken ct = default);
    Task<MealCompletionResultDto> SaveMealMissedReasonAsync(Guid traineeId, SaveMealMissedReasonDto dto, CancellationToken ct = default);
    Task<MealCompletionResultDto> ReviewMealCompletionAsync(Guid coachId, CoachReviewMealDto dto, CancellationToken ct = default);
    Task<CoachTraineeMealProgressDto> GetCoachTraineeMealProgressAsync(Guid coachId, Guid orderDetailsId, CancellationToken ct = default);
}
