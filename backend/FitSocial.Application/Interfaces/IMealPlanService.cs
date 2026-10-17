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
}
