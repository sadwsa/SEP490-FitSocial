using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

/// <summary>
/// Repository interface for querying Coach Subscription Plan price change histories.
/// </summary>
public interface ISubscriptionPriceHistoryRepository
{
    Task<(List<SubscriptionPriceHistoryItem> Items, int TotalCount)> GetPriceHistoryAsync(
        Guid? planId,
        string? keyword,
        DateTime? fromDate,
        DateTime? toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}
