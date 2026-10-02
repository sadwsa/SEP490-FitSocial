using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemOperations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class SystemOperationsService : ISystemOperationsService
{
    private readonly ICoachSubscriptionPlanRepository _planRepository;
    private readonly ICoachUpgradeRepository _upgrades;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SystemOperationsService> _logger;

    public SystemOperationsService(
        ICoachSubscriptionPlanRepository planRepository,
        ICoachUpgradeRepository upgrades,
        IUnitOfWork unitOfWork,
        ILogger<SystemOperationsService> logger)
    {
        _planRepository = planRepository;
        _upgrades = upgrades;
        _unitOfWork = unitOfWork;
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
            var allPlans = await _planRepository.GetAllPlansAsync(cancellationToken);
            var query = allPlans.AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p => 
                    (p.Currency != null && p.Currency.ToLower().Contains(s)) || 
                    (p.Amount.HasValue && p.Amount.Value.ToString().Contains(s)) || 
                    (p.Description != null && p.Description.ToLower().Contains(s)));
            }

            var totalPlans = query.Count();
            var activePlansCount = allPlans.Count(p => p.IsActive == true);
            var pagedPlans = query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100))
                .Take(Math.Clamp(pageSize, 1, 100))
                .ToList();

            var planIds = pagedPlans.Select(p => p.CoachSubscriptionPlansId).ToList();
            var countDict = await _upgrades.GetCountsByPriceIdsAsync(planIds, cancellationToken);

            var plans = pagedPlans.Select(p => new CoachSubscriptionPlanDto
            {
                CoachSubscriptionPlansId = p.CoachSubscriptionPlansId,
                PriceId = p.PriceId,
                Amount = p.Amount ?? 0m,
                Currency = p.Currency ?? "VND",
                IsActive = p.IsActive ?? false,
                ImageUrl = p.ImageUrl,
                Description = p.Description,
                SubscriptionDuration = p.SubscriptionDuration,
                TrainingPackageDuration = p.TrainingPackageDuration,
                CreatedAt = p.CreatedAt ?? DateTime.UtcNow,
                SubscriberCount = countDict.TryGetValue(p.CoachSubscriptionPlansId, out var c) ? c.Total : 0,
                ActiveSubscriberCount = countDict.TryGetValue(p.CoachSubscriptionPlansId, out var c2) ? c2.Active : 0
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
        var plan = await _planRepository.GetByIdAsync(priceId, cancellationToken);
        if (plan == null)
            return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Subscription plan not found.");

        var count = await _upgrades.CountByPriceIdAsync(priceId, cancellationToken);
        var activeCount = await _upgrades.CountActiveByPriceIdAsync(priceId, cancellationToken);

        var dto = new CoachSubscriptionPlanDto
        {
            CoachSubscriptionPlansId = plan.CoachSubscriptionPlansId,
            PriceId = plan.PriceId,
            Amount = plan.Amount ?? 0m,
            Currency = plan.Currency ?? "VND",
            IsActive = plan.IsActive ?? false,
            ImageUrl = plan.ImageUrl,
            Description = plan.Description,
            SubscriptionDuration = plan.SubscriptionDuration,
            TrainingPackageDuration = plan.TrainingPackageDuration,
            CreatedAt = plan.CreatedAt ?? DateTime.UtcNow,
            SubscriberCount = count,
            ActiveSubscriberCount = activeCount
        };
        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(dto);
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> CreatePlanAsync(CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = new FitSocial.Domain.Entities.CoachSubscriptionPlan
            {
                CoachSubscriptionPlansId = Guid.NewGuid(),
                Amount = dto.Amount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "VND" : dto.Currency.Trim().ToUpper(),
                IsActive = dto.IsActive,
                ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                SubscriptionDuration = dto.SubscriptionDuration,
                TrainingPackageDuration = dto.TrainingPackageDuration,
                CreatedAt = DateTime.UtcNow
            };

            await _planRepository.AddAsync(plan, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = new CoachSubscriptionPlanDto
            {
                CoachSubscriptionPlansId = plan.CoachSubscriptionPlansId,
                PriceId = plan.PriceId,
                Amount = plan.Amount ?? 0m,
                Currency = plan.Currency ?? "VND",
                IsActive = plan.IsActive ?? false,
                ImageUrl = plan.ImageUrl,
                Description = plan.Description,
                SubscriptionDuration = plan.SubscriptionDuration,
                TrainingPackageDuration = plan.TrainingPackageDuration,
                CreatedAt = plan.CreatedAt ?? DateTime.UtcNow,
                SubscriberCount = 0,
                ActiveSubscriberCount = 0
            };
            return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(resultDto, "Subscription plan created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create coach subscription plan");
            return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Could not create subscription plan.");
        }
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid planId, UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
            if (plan == null)
                return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Subscription plan not found.");

            if (!dto.Amount.HasValue || dto.Amount.Value < 0)
                return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Amount must be >= 0.");
            if (string.IsNullOrWhiteSpace(dto.Description))
                return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Description is required.");

            plan.Amount = dto.Amount;
            plan.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? plan.Currency : dto.Currency.Trim().ToUpper();
            plan.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            plan.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
            if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;
            if (dto.SubscriptionDuration.HasValue) plan.SubscriptionDuration = dto.SubscriptionDuration.Value;
            if (dto.TrainingPackageDuration.HasValue) plan.TrainingPackageDuration = dto.TrainingPackageDuration.Value;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new CoachSubscriptionPlanDto
            {
                CoachSubscriptionPlansId = plan.CoachSubscriptionPlansId,
                PriceId = plan.PriceId,
                Amount = plan.Amount ?? 0m,
                Currency = plan.Currency ?? "VND",
                IsActive = plan.IsActive ?? false,
                ImageUrl = plan.ImageUrl,
                Description = plan.Description,
                SubscriptionDuration = plan.SubscriptionDuration,
                TrainingPackageDuration = plan.TrainingPackageDuration,
                CreatedAt = plan.CreatedAt ?? DateTime.UtcNow,
                SubscriberCount = 0,
                ActiveSubscriberCount = 0
            };

            // Active subscribers keep their historical tier: CoachUpgrade holds the price snapshot at purchase
            // time (Order/Payment rows), so changing this plan does NOT alter past transactions.
            return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(result, "Subscription plan updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update subscription plan {PlanId}", planId);
            return ApiResponseDto<CoachSubscriptionPlanDto>.Fail("Could not update subscription plan.");
        }
    }

    public async Task<ApiResponseDto<bool>> DeletePlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await _planRepository.GetByIdAsync(planId, cancellationToken);
            if (plan == null)
                return ApiResponseDto<bool>.Fail("Subscription plan not found.");

            // 33.3.E1: preserve financial audit logs — never hard-delete referenced plans.
            var (upgrades, details) = await _planRepository.CountReferencesAsync(planId, cancellationToken);
            if (upgrades > 0 || details > 0)
            {
                plan.IsActive = false;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return ApiResponseDto<bool>.Ok(true,
                    $"Plan has {upgrades + details} linked subscription record(s); it was deactivated instead of deleted to preserve billing history.");
            }

            _planRepository.Remove(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ApiResponseDto<bool>.Ok(true, "Subscription plan deleted successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete subscription plan {PlanId}", planId);
            return ApiResponseDto<bool>.Fail("Could not delete subscription plan.");
        }
    }
}

