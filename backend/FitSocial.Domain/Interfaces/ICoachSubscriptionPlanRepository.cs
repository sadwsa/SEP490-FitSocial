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
    Task<(List<SubscriptionPriceHistoryItem> Items, int TotalCount)> GetPriceHistoryAsync(
        Guid? planId,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}

