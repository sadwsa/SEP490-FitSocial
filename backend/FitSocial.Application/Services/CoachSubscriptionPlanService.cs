using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachSubscriptionPlans;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class CoachSubscriptionPlanService : ICoachSubscriptionPlanService
{
    private readonly ICoachSubscriptionPlanRepository _planRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public CoachSubscriptionPlanService(
        ICoachSubscriptionPlanRepository planRepository,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService)
    {
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponseDto<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository.ListActivePlansAsync(cancellationToken);
        var dtos = plans.Select(MapToDto).ToList();
        return ApiResponseDto<List<CoachSubscriptionPlanDto>>.Ok(dtos, "Active subscription plans retrieved.");
    }

    public async Task<ApiResponseDto<List<CoachSubscriptionPlanDto>>> GetAllPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _planRepository.GetAllPlansAsync(cancellationToken);
        var dtos = plans.OrderByDescending(p => p.CreatedAt).Select(MapToDto).ToList();
        return ApiResponseDto<List<CoachSubscriptionPlanDto>>.Ok(dtos, "All subscription plans retrieved.");
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("Coach subscription plan not found.");
        }

        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(MapToDto(plan));
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> CreatePlanAsync(CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ValidationException("Plan payload cannot be null.");
        }

        var now = DateTime.UtcNow;
        var plan = new CoachSubscriptionPlan
        {
            CoachSubscriptionPlansId = Guid.NewGuid(),
            PriceId = dto.PriceId,
            Amount = dto.Amount,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "VND" : dto.Currency.Trim().ToUpperInvariant(),
            Description = dto.Description?.Trim(),
            SubscriptionDuration = dto.SubscriptionDuration,
            TrainingPackageDuration = dto.TrainingPackageDuration,
            ImageUrl = dto.ImageUrl?.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = now
        };

        await _planRepository.AddAsync(plan, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "CREATE_COACH_SUBSCRIPTION_PLAN",
            entityId: plan.CoachSubscriptionPlansId,
            description: $"Coach subscription plan '{plan.Description}' ({plan.Amount:N0} {plan.Currency}) was created.",
            oldValue: null,
            newValue: new
            {
                priceId = plan.PriceId,
                amount = plan.Amount,
                currency = plan.Currency,
                description = plan.Description,
                subscriptionDuration = plan.SubscriptionDuration,
                trainingPackageDuration = plan.TrainingPackageDuration,
                imageUrl = plan.ImageUrl,
                isActive = plan.IsActive
            },
            cancellationToken);

        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(MapToDto(plan), "Coach subscription plan created successfully.");
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("Coach subscription plan not found.");
        }

        var oldValues = new
        {
            priceId = plan.PriceId,
            amount = plan.Amount,
            currency = plan.Currency,
            description = plan.Description,
            subscriptionDuration = plan.SubscriptionDuration,
            trainingPackageDuration = plan.TrainingPackageDuration,
            imageUrl = plan.ImageUrl,
            isActive = plan.IsActive
        };
        var oldAmount = plan.Amount;

        if (dto.Amount.HasValue) plan.Amount = dto.Amount.Value;
        if (!string.IsNullOrWhiteSpace(dto.Currency)) plan.Currency = dto.Currency.Trim().ToUpperInvariant();
        if (dto.Description != null) plan.Description = dto.Description.Trim();
        if (dto.SubscriptionDuration.HasValue) plan.SubscriptionDuration = dto.SubscriptionDuration.Value;
        if (dto.TrainingPackageDuration.HasValue) plan.TrainingPackageDuration = dto.TrainingPackageDuration.Value;
        if (dto.ImageUrl != null) plan.ImageUrl = dto.ImageUrl.Trim();
        if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "UPDATE_COACH_SUBSCRIPTION_PLAN",
            entityId: plan.CoachSubscriptionPlansId,
            description: $"Coach subscription plan '{plan.Description}' was updated ({oldAmount:N0} -> {plan.Amount:N0} {plan.Currency}).",
            oldValue: oldValues,
            newValue: new
            {
                priceId = plan.PriceId,
                amount = plan.Amount,
                currency = plan.Currency,
                description = plan.Description,
                subscriptionDuration = plan.SubscriptionDuration,
                trainingPackageDuration = plan.TrainingPackageDuration,
                imageUrl = plan.ImageUrl,
                isActive = plan.IsActive
            },
            cancellationToken);

        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(MapToDto(plan), "Coach subscription plan updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("Coach subscription plan not found.");
        }

        var oldValues = new
        {
            description = plan.Description,
            amount = plan.Amount,
            currency = plan.Currency,
            isActive = plan.IsActive
        };

        // Soft-delete
        plan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "DELETE_COACH_SUBSCRIPTION_PLAN",
            entityId: plan.CoachSubscriptionPlansId,
            description: $"Coach subscription plan '{oldValues.description}' was deleted (deactivated).",
            oldValue: oldValues,
            newValue: new { isActive = false },
            cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Coach subscription plan deactivated successfully.");
    }

    /// <summary>
    /// UC_33.2/33.3/33.4: every create/edit/delete of a coach subscription plan is
    /// traced in the shared "AuditLogs" table. Actor (Staff/Admin), role and IP are
    /// auto-resolved from the current request by IAuditLogService.
    /// </summary>
    private async Task WriteAuditAsync(
        string action,
        Guid entityId,
        string description,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogService.LogAsync(
                actorAccountId: null, // Auto-resolved from ICurrentUserContext
                action: action,
                entityType: "CoachSubscriptionPlan",
                entityId: entityId.ToString(),
                description: description,
                oldValue: oldValue,
                newValue: newValue,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Audit log failure is non-fatal - the business operation already succeeded.
        }
    }

    private static CoachSubscriptionPlanDto MapToDto(CoachSubscriptionPlan p)
    {
        return new CoachSubscriptionPlanDto
        {
            CoachSubscriptionPlansId = p.CoachSubscriptionPlansId,
            PriceId = p.PriceId,
            Amount = p.Amount,
            Currency = p.Currency,
            Description = p.Description,
            SubscriptionDuration = p.SubscriptionDuration,
            TrainingPackageDuration = p.TrainingPackageDuration,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt
        };
    }
}
