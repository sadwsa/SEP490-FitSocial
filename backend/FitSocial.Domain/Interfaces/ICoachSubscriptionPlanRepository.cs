using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface ICoachSubscriptionPlanRepository : IRepository<CoachSubscriptionPlan>
{
    Task<List<CoachSubscriptionPlan>> ListActivePlansAsync(CancellationToken cancellationToken = default);
    Task<List<CoachSubscriptionPlan>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    Task<CoachSubscriptionPlan?> GetActivePlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>UC_33.3: references blocking hard-delete (upgrades + order details).</summary>
    Task<(int Upgrades, int OrderDetails)> CountReferencesAsync(Guid planId, CancellationToken cancellationToken = default);
}
