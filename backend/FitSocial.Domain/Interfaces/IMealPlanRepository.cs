using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IMealPlanRepository
{
    Task<MealPlan?> GetMealPlanByOrderDetailsAsync(Guid orderDetailsId, CancellationToken ct = default);
    Task<MealPlan?> GetByIdWithItemsAsync(Guid mealPlanId, bool asNoTracking = false, CancellationToken ct = default);
    Task<OrderDetail?> GetOrderDetailWithOrderAndPackageAsync(Guid orderDetailsId, CancellationToken ct = default);
    Task AddAsync(MealPlan mealPlan, CancellationToken ct = default);
    void RemoveItems(IEnumerable<MealPlanItem> items);
    Task AddItemAsync(MealPlanItem item, CancellationToken ct = default);

    // Meal Completion & Coach Review
    Task<MealPlanItem?> GetMealItemByIdAsync(Guid mealPlanItemId, CancellationToken ct = default);
    Task<MealCompletionLog?> GetMealLogAsync(Guid mealPlanItemId, Guid traineeId, CancellationToken ct = default);
    Task AddMealLogAsync(MealCompletionLog log, CancellationToken ct = default);
    Task<MealPlan?> GetCoachMealPlanWithLogsAsync(Guid orderDetailsId, Guid coachId, CancellationToken ct = default);
}
