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

    public CoachSubscriptionPlanService(
        ICoachSubscriptionPlanRepository planRepository,
        IUnitOfWork unitOfWork)
    {
        _planRepository = planRepository;
        _unitOfWork = unitOfWork;
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

        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(MapToDto(plan), "Coach subscription plan created successfully.");
    }

    public async Task<ApiResponseDto<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid id, UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("Coach subscription plan not found.");
        }

        if (dto.Amount.HasValue) plan.Amount = dto.Amount.Value;
        if (!string.IsNullOrWhiteSpace(dto.Currency)) plan.Currency = dto.Currency.Trim().ToUpperInvariant();
        if (dto.Description != null) plan.Description = dto.Description.Trim();
        if (dto.SubscriptionDuration.HasValue) plan.SubscriptionDuration = dto.SubscriptionDuration.Value;
        if (dto.TrainingPackageDuration.HasValue) plan.TrainingPackageDuration = dto.TrainingPackageDuration.Value;
        if (dto.ImageUrl != null) plan.ImageUrl = dto.ImageUrl.Trim();
        if (dto.IsActive.HasValue) plan.IsActive = dto.IsActive.Value;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<CoachSubscriptionPlanDto>.Ok(MapToDto(plan), "Coach subscription plan updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, cancellationToken);
        if (plan == null)
        {
            throw new NotFoundException("Coach subscription plan not found.");
        }

        // Soft-delete
        plan.IsActive = false;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Coach subscription plan deactivated successfully.");
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
