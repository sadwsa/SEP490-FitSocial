using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IMealPlanRepository
{
    Task<MealPlan?> GetMealPlanByOrderDetailsAsync(Guid orderDetailsId, CancellationToken ct = default);
}
