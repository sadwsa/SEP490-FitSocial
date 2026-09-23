using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemOperations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class SystemOperationsService : ISystemOperationsService
{
    private readonly IPriceRepository _prices;
    private readonly ICoachUpgradeRepository _upgrades;
    private readonly ILogger<SystemOperationsService> _logger;

    public SystemOperationsService(IPriceRepository prices, ICoachUpgradeRepository upgrades, ILogger<SystemOperationsService> logger)
    {
        _prices = prices;
        _upgrades = upgrades;
        _logger = logger;
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(
        bool? isActive = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (prices, totalPlans) = await _prices.ListPagedAsync(isActive, search, page, pageSize, cancellationToken);
            var activePlansCount = await _prices.CountActiveAsync(cancellationToken);
            var priceIds = prices.Select(p => p.PriceId).ToList();
            var countDict = await _upgrades.GetCountsByPriceIdsAsync(priceIds, cancellationToken);

            var plans = prices.Select(p => new CoachSubscriptionPlanDto
            {
                PriceId = p.PriceId,
                Amount = p.Amount ?? 0,
                Currency = p.Currency ?? "VND",
                IsActive = p.IsActive ?? false,
                ImageUrl = p.ImageUrl,
                Description = p.Description,
                CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                SubscriberCount = countDict.TryGetValue(p.PriceId, out var c) ? c.Total : 0,
                ActiveSubscriberCount = countDict.TryGetValue(p.PriceId, out var c2) ? c2.Active : 0
            }).ToList();

            var result = new CoachSubscriptionPlansResponseDto
            {
                Plans = plans,
                TotalPlans = totalPlans,
                ActivePlansCount = activePlansCount
            };

            return ApiResponseDto<CoachSubscriptionPlansResponseDto>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get coach subscription plans");
            return ApiResponseDto<CoachSubscriptionPlansResponseDto>.Fail("Could not load subscription plans.");
        }
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId, CancellationToken cancellationToken = default)
    {
        var price = await _prices.GetByIdAsync(priceId, cancellationToken);
        if (price == null)
            return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Subscription plan not found.");

        var count = await _upgrades.CountByPriceIdAsync(priceId, cancellationToken);
        var activeCount = await _upgrades.CountActiveByPriceIdAsync(priceId, cancellationToken);

        var dto = new CoachSubscriptionPlanDto
        {
            PriceId = price.PriceId,
            Amount = price.Amount ?? 0,
            Currency = price.Currency ?? "VND",
            IsActive = price.IsActive ?? false,
            ImageUrl = price.ImageUrl,
            Description = price.Description,
            CreatedAt = price.CreatedAt ?? DateTime.UtcNow,
            SubscriberCount = count,
            ActiveSubscriberCount = activeCount
        };
        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(dto);
    }
}
