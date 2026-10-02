using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SubscriptionPriceHistory;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

/// <summary>
/// Service implementation for managing and viewing subscription price history.
/// </summary>
public class SubscriptionPriceHistoryService : ISubscriptionPriceHistoryService
{
    private readonly ISubscriptionPriceHistoryRepository _priceHistoryRepository;

    public SubscriptionPriceHistoryService(ISubscriptionPriceHistoryRepository priceHistoryRepository)
    {
        _priceHistoryRepository = priceHistoryRepository;
    }

    public async Task<ApiResponseDto<PagedResultDto<SubscriptionPriceHistoryDto>>> GetPriceHistoryAsync(
        GetPriceHistoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new GetPriceHistoryFilterDto();

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate.Value > filter.ToDate.Value)
        {
            throw new ValidationException("FromDate cannot be greater than ToDate.");
        }

        var pageIndex = Math.Max(1, filter.PageIndex);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var (items, totalCount) = await _priceHistoryRepository.GetPriceHistoryAsync(
            filter.PlanId,
            filter.Keyword,
            filter.FromDate,
            filter.ToDate,
            pageIndex,
            pageSize,
            cancellationToken);

        var dtos = items.Select(item =>
        {
            var priceDifference = item.NewPrice - item.OldPrice;
            decimal percentageChange;

            if (item.OldPrice == 0)
            {
                if (item.NewPrice == 0)
                {
                    percentageChange = 0m;
                }
                else if (item.NewPrice > 0)
                {
                    percentageChange = 100m;
                }
                else
                {
                    percentageChange = -100m;
                }
            }
            else
            {
                percentageChange = Math.Round(((item.NewPrice - item.OldPrice) / item.OldPrice) * 100m, 2);
            }

            return new SubscriptionPriceHistoryDto
            {
                HistoryId = item.HistoryId,
                PlanId = item.PlanId,
                PlanName = string.IsNullOrWhiteSpace(item.PlanName) ? "Coach Subscription Plan" : item.PlanName,
                OldPrice = item.OldPrice,
                NewPrice = item.NewPrice,
                PriceDifference = priceDifference,
                PercentageChange = percentageChange,
                ChangedAt = item.ChangedAt,
                ChangedByUserId = item.ChangedByUserId,
                ChangedByName = item.ChangedByName ?? "System Admin",
                Notes = item.Notes
            };
        }).ToList();

        var pagedResult = PagedResultDto<SubscriptionPriceHistoryDto>.Create(dtos, totalCount, pageIndex, pageSize);

        return ApiResponseDto<PagedResultDto<SubscriptionPriceHistoryDto>>.Ok(
            pagedResult,
            "Subscription price history retrieved successfully.");
    }
}
