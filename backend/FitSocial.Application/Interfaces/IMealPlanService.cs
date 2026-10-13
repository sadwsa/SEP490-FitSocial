using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;

namespace FitSocial.Application.Interfaces;

public interface IMealPlanService
{
    Task<TraineeMealPlanDto?> GetTraineeMealPlanAsync(Guid traineeId, Guid orderDetailsId, CancellationToken ct = default);
}
