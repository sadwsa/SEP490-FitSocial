using System;
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
}
