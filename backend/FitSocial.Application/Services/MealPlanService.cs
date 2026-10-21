using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class MealPlanService : IMealPlanService
{
    private readonly IMealPlanRepository _mealPlanRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MealPlanService(
        IMealPlanRepository mealPlanRepository,
        IUnitOfWork unitOfWork)
    {
        _mealPlanRepository = mealPlanRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TraineeMealPlanDto?> GetTraineeMealPlanAsync(
        Guid traineeId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
            throw new ForbiddenException("Invalid trainee identity.");

        var plan = await _mealPlanRepository.GetMealPlanByOrderDetailsAsync(orderDetailsId, ct);
        if (plan == null) return null;

        if (plan.OrderDetails?.Order != null && plan.OrderDetails.Order.BuyerId != traineeId)
        {
            throw new ForbiddenException("You do not have permission to view this meal plan.");
        }

        return MapToDto(plan);
    }

    public async Task<TraineeMealPlanDto?> GetCoachMealPlanByOrderDetailsAsync(
        Guid coachId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
            throw new ForbiddenException("Invalid coach identity.");

        var plan = await _mealPlanRepository.GetMealPlanByOrderDetailsAsync(orderDetailsId, ct);
        if (plan == null) return null;

        var isOwner = (plan.OrderDetails?.Order != null && plan.OrderDetails.Order.CoachId == coachId) ||
                      (plan.OrderDetails?.Package != null && plan.OrderDetails.Package.CoachId == coachId) ||
                      plan.CoachId == coachId;

        if (!isOwner)
        {
            throw new ForbiddenException("You do not have permission to view meal plan for this order.");
        }

        return MapToDto(plan);
    }

    public async Task<TraineeMealPlanDto> CreateMealPlanAsync(
        Guid coachId,
        CreateMealPlanDto dto,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
            throw new ForbiddenException("Invalid coach identity.");

        if (dto == null)
            throw new ValidationException("Request body cannot be null.");

        if (dto.OrderDetailsId == Guid.Empty)
            throw new ValidationException("OrderDetailsId is required.");

        var orderDetail = await _mealPlanRepository.GetOrderDetailWithOrderAndPackageAsync(dto.OrderDetailsId, ct);
        if (orderDetail == null)
            throw new NotFoundException("Order details not found.");

        var isOwner = (orderDetail.Order != null && orderDetail.Order.CoachId == coachId) ||
                      (orderDetail.Package != null && orderDetail.Package.CoachId == coachId);

        if (!isOwner)
            throw new ForbiddenException("You do not have permission to manage meal plans for this order.");

        var packageType = orderDetail.PackageType ?? orderDetail.Package?.PackageType ?? "BOTH";
        if (string.Equals(packageType, "WORKOUT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("This package is WORKOUT only and does not include a nutrition/meal plan.");
        }

        // Validate plan constraints against order and package
        ValidatePlanConstraints(orderDetail, dto.StartDate, dto.EndDate, dto.Items);

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var plan = new MealPlan
        {
            MealPlanId = Guid.NewGuid(),
            CoachId = coachId,
            OrderDetailsId = dto.OrderDetailsId,
            OrderDetails = orderDetail,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? "Personalized Nutrition & Meal Plan" : dto.Title.Trim(),
            Description = dto.Description,
            StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Unspecified) : null,
            EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Unspecified) : null,
            Status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status : "ACTIVE",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = (string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(dto.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) ? now : null
        };

        if (dto.Items != null && dto.Items.Count > 0)
        {
            short defaultSort = 1;
            foreach (var itemDto in dto.Items)
            {
                var item = new MealPlanItem
                {
                    MealPlanItemId = Guid.NewGuid(),
                    MealPlanId = plan.MealPlanId,
                    WeekNumber = itemDto.WeekNumber,
                    DayNumber = itemDto.DayNumber,
                    MealType = itemDto.MealType?.Trim() ?? "Meal",
                    FoodDescription = itemDto.FoodDescription?.Trim() ?? string.Empty,
                    Calories = itemDto.Calories,
                    SortOrder = itemDto.SortOrder ?? defaultSort++
                };

                plan.MealPlanItems.Add(item);
            }
        }

        await _mealPlanRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var created = await _mealPlanRepository.GetByIdWithItemsAsync(plan.MealPlanId, asNoTracking: true, ct);
        return MapToDto(created ?? plan);
    }

    public async Task<TraineeMealPlanDto> UpdateMealPlanAsync(
        Guid coachId,
        Guid planId,
        UpdateMealPlanDto dto,
        CancellationToken ct = default)
    {
        return await UpdateMealPlanInternalAsync(coachId, planId, dto, asOfDate: null, ct);
    }

    public async Task<TraineeMealPlanDto> UpdateMealPlanInternalAsync(
        Guid coachId,
        Guid planId,
        UpdateMealPlanDto dto,
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
            throw new ForbiddenException("Invalid coach identity.");

        if (dto == null)
            throw new ValidationException("Request body cannot be null.");

        var existingPlan = await _mealPlanRepository.GetByIdWithItemsAsync(planId, asNoTracking: false, ct);
        if (existingPlan == null)
            throw new NotFoundException("Meal plan not found.");

        if (existingPlan.CoachId != coachId)
            throw new ForbiddenException("You do not have permission to update this meal plan.");

        if (existingPlan.OrderDetails != null)
        {
            var isOrderOwner = (existingPlan.OrderDetails.Order != null && existingPlan.OrderDetails.Order.CoachId == coachId) ||
                               (existingPlan.OrderDetails.Package != null && existingPlan.OrderDetails.Package.CoachId == coachId);

            if (!isOrderOwner)
                throw new ForbiddenException("You do not have permission to update meal plan for this order.");
        }
        else if (existingPlan.OrderDetailsId.HasValue)
        {
            existingPlan.OrderDetails = await _mealPlanRepository.GetOrderDetailWithOrderAndPackageAsync(existingPlan.OrderDetailsId.Value, ct);
        }

        var effectiveStartDate = dto.StartDate ?? existingPlan.StartDate;
        var effectiveEndDate = dto.EndDate ?? existingPlan.EndDate;

        var itemsToCheck = dto.Items != null
            ? dto.Items.Select(i => new CreateMealPlanItemDto
            {
                WeekNumber = i.WeekNumber,
                DayNumber = i.DayNumber,
                MealType = i.MealType,
                FoodDescription = i.FoodDescription,
                Calories = i.Calories,
                SortOrder = i.SortOrder
            }).ToList()
            : existingPlan.MealPlanItems.Select(i => new CreateMealPlanItemDto
            {
                WeekNumber = i.WeekNumber ?? 1,
                DayNumber = i.DayNumber ?? 1,
                MealType = i.MealType ?? "Meal",
                FoodDescription = i.FoodDescription ?? string.Empty,
                Calories = i.Calories,
                SortOrder = i.SortOrder
            }).ToList();

        var today = (asOfDate ?? DateTime.Today).Date;

        ValidatePlanConstraints(existingPlan.OrderDetails, effectiveStartDate, effectiveEndDate, itemsToCheck, isUpdate: true, asOfDate: today);

        // Rule: Start Date cannot be altered once meal plan has already started / past items exist
        if (dto.StartDate.HasValue && existingPlan.StartDate.HasValue && dto.StartDate.Value.Date != existingPlan.StartDate.Value.Date)
        {
            var hasStartedOrPast = existingPlan.StartDate.Value.Date <= today ||
                existingPlan.MealPlanItems.Any(e =>
                    GetSessionDate(existingPlan.StartDate.Value, e.WeekNumber ?? 1, e.DayNumber ?? 1).Date <= today);

            if (hasStartedOrPast)
            {
                throw new ValidationException("Cannot change Start Date once meal plan dates have already started or occurred.");
            }
        }

        // Rule: Cannot delete or modify meal items scheduled for today or in the past
        if (dto.Items != null && existingPlan.StartDate.HasValue)
        {
            var baseStart = existingPlan.StartDate.Value;
            foreach (var existingItem in existingPlan.MealPlanItems)
            {
                var itemDate = GetSessionDate(baseStart, existingItem.WeekNumber ?? 1, existingItem.DayNumber ?? 1);
                if (itemDate.Date <= today)
                {
                    var matchedIncoming = dto.Items.FirstOrDefault(i =>
                        i.MealPlanItemId.HasValue && i.MealPlanItemId.Value == existingItem.MealPlanItemId);

                    if (matchedIncoming == null)
                    {
                        // Fallback matching: If client omitted MealPlanItemId, match unlinked item on the same week, day and meal type
                        matchedIncoming = dto.Items.FirstOrDefault(i =>
                            !i.MealPlanItemId.HasValue &&
                            i.WeekNumber == (existingItem.WeekNumber ?? 1) &&
                            i.DayNumber == (existingItem.DayNumber ?? 1) &&
                            string.Equals(i.MealType?.Trim(), existingItem.MealType?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(i.FoodDescription?.Trim(), existingItem.FoodDescription?.Trim(), StringComparison.Ordinal));

                        if (matchedIncoming == null)
                        {
                            matchedIncoming = dto.Items.FirstOrDefault(i =>
                                !i.MealPlanItemId.HasValue &&
                                i.WeekNumber == (existingItem.WeekNumber ?? 1) &&
                                i.DayNumber == (existingItem.DayNumber ?? 1) &&
                                string.Equals(i.MealType?.Trim(), existingItem.MealType?.Trim(), StringComparison.OrdinalIgnoreCase));
                        }

                        if (matchedIncoming != null)
                        {
                            matchedIncoming.MealPlanItemId = existingItem.MealPlanItemId;
                        }
                    }

                    if (matchedIncoming == null)
                    {
                        throw new ValidationException(
                            $"Cannot delete meal item on {itemDate:dd/MM/yyyy} ({existingItem.MealType}: {existingItem.FoodDescription}) as it is scheduled for today or has already occurred.");
                    }

                    bool isModified =
                        !string.Equals(matchedIncoming.FoodDescription?.Trim(), existingItem.FoodDescription?.Trim(), StringComparison.Ordinal) ||
                        !string.Equals(matchedIncoming.MealType?.Trim(), existingItem.MealType?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        matchedIncoming.WeekNumber != (existingItem.WeekNumber ?? 1) ||
                        matchedIncoming.DayNumber != (existingItem.DayNumber ?? 1) ||
                        matchedIncoming.Calories != existingItem.Calories;

                    if (isModified)
                    {
                        throw new ValidationException(
                            $"Cannot modify meal item on {itemDate:dd/MM/yyyy} ({existingItem.MealType}: {existingItem.FoodDescription}) as it is scheduled for today or has already occurred.");
                    }
                }
            }
        }

        // Apply metadata updates
        if (dto.Title != null) existingPlan.Title = dto.Title.Trim();
        if (dto.Description != null) existingPlan.Description = dto.Description;
        if (dto.StartDate.HasValue) existingPlan.StartDate = DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Unspecified);
        if (dto.EndDate.HasValue) existingPlan.EndDate = DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Unspecified);
        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            existingPlan.Status = dto.Status;
            if (string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) && !existingPlan.PublishedAt.HasValue)
            {
                existingPlan.PublishedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            }
        }
        existingPlan.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Apply item updates
        if (dto.Items != null)
        {
            var incomingIds = dto.Items.Where(i => i.MealPlanItemId.HasValue).Select(i => i.MealPlanItemId!.Value).ToHashSet();
            var itemsToRemove = existingPlan.MealPlanItems.Where(i => !incomingIds.Contains(i.MealPlanItemId)).ToList();
            _mealPlanRepository.RemoveItems(itemsToRemove);

            short defaultSort = 1;
            foreach (var itemDto in dto.Items)
            {
                if (itemDto.MealPlanItemId.HasValue)
                {
                    var existingItem = existingPlan.MealPlanItems.FirstOrDefault(i => i.MealPlanItemId == itemDto.MealPlanItemId.Value);
                    if (existingItem != null)
                    {
                        existingItem.WeekNumber = itemDto.WeekNumber;
                        existingItem.DayNumber = itemDto.DayNumber;
                        existingItem.MealType = itemDto.MealType?.Trim() ?? existingItem.MealType;
                        existingItem.FoodDescription = itemDto.FoodDescription?.Trim() ?? existingItem.FoodDescription;
                        existingItem.Calories = itemDto.Calories;
                        existingItem.SortOrder = itemDto.SortOrder ?? existingItem.SortOrder ?? defaultSort++;
                        continue;
                    }
                }

                var newItem = new MealPlanItem
                {
                    MealPlanItemId = Guid.NewGuid(),
                    MealPlanId = existingPlan.MealPlanId,
                    WeekNumber = itemDto.WeekNumber,
                    DayNumber = itemDto.DayNumber,
                    MealType = itemDto.MealType?.Trim() ?? "Meal",
                    FoodDescription = itemDto.FoodDescription?.Trim() ?? string.Empty,
                    Calories = itemDto.Calories,
                    SortOrder = itemDto.SortOrder ?? defaultSort++
                };
                existingPlan.MealPlanItems.Add(newItem);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _mealPlanRepository.GetByIdWithItemsAsync(planId, asNoTracking: true, ct);
        return MapToDto(updated ?? existingPlan);
    }

    public static DateTime GetStartMonday(DateTime startDate)
    {
        int dayOfWeek = (int)startDate.DayOfWeek;
        int daysSinceMonday = (dayOfWeek == 0 ? 7 : dayOfWeek) - 1;
        return startDate.Date.AddDays(-daysSinceMonday);
    }

    public static DateTime GetSessionDate(DateTime startDate, short weekNumber, short dayNumber)
    {
        var startMonday = GetStartMonday(startDate);
        return startMonday.AddDays((weekNumber - 1) * 7 + (dayNumber - 1));
    }

    public static void ValidatePlanConstraints(
        OrderDetail? orderDetail,
        DateTime? startDate,
        DateTime? endDate,
        IEnumerable<CreateMealPlanItemDto>? items,
        bool isUpdate = false,
        DateTime? asOfDate = null)
    {
        var itemList = items?.ToList() ?? new List<CreateMealPlanItemDto>();

        if (startDate.HasValue && endDate.HasValue)
        {
            if (endDate.Value.Date < startDate.Value.Date)
            {
                throw new ValidationException("End Date must be on or after Start Date.");
            }

            var durationDays = orderDetail?.PackageDurationDays ?? orderDetail?.Package?.DurationDays;
            var packagePurchasedAt = orderDetail?.Order?.CreatedAt ?? orderDetail?.CreatedAt;

            if (packagePurchasedAt.HasValue && packagePurchasedAt.Value != default(DateTime))
            {
                var packageStartDate = packagePurchasedAt.Value.Date;
                if (startDate.Value.Date < packageStartDate)
                {
                    throw new ValidationException($"Start Date ({startDate.Value:dd/MM/yyyy}) cannot be earlier than package purchase date ({packageStartDate:dd/MM/yyyy}).");
                }

                if (durationDays.HasValue && durationDays.Value > 0)
                {
                    var packageEndDate = packageStartDate.AddDays(durationDays.Value);
                    if (endDate.Value.Date > packageEndDate)
                    {
                        throw new ValidationException($"End Date ({endDate.Value:dd/MM/yyyy}) cannot exceed package expiration date ({packageEndDate:dd/MM/yyyy}).");
                    }
                }
            }

            if (durationDays.HasValue && durationDays.Value > 0)
            {
                var planDays = (int)(endDate.Value.Date - startDate.Value.Date).TotalDays + 1;
                if (planDays > durationDays.Value + 1)
                {
                    throw new ValidationException($"Meal plan duration ({planDays} days) cannot exceed purchased package duration ({durationDays.Value} days).");
                }
            }

            var totalDays = (int)(endDate.Value.Date - startDate.Value.Date).TotalDays + 1;
            var maxWeeks = Math.Max(1, totalDays / 7);

            if (itemList.Any(e => e.WeekNumber > maxWeeks))
            {
                var highest = itemList.Max(e => e.WeekNumber);
                throw new ValidationException($"Meal plan cannot exceed {maxWeeks} weeks for the selected {totalDays}-day period (highest week found: {highest}).");
            }

            var effectiveDate = (asOfDate ?? DateTime.Today).Date;
            var startMonday = GetStartMonday(startDate.Value.Date);

            short upcomingWeek;
            if (!isUpdate || effectiveDate < startMonday)
            {
                upcomingWeek = 1;
            }
            else
            {
                var currentWeek = (short)(((effectiveDate - startMonday).Days / 7) + 1);
                upcomingWeek = (short)(currentWeek + 1);
            }

            if (upcomingWeek <= maxWeeks)
            {
                var hasUpcomingWeekMeal = itemList.Any(e =>
                    e.WeekNumber == upcomingWeek && !string.IsNullOrWhiteSpace(e.FoodDescription));

                if (!hasUpcomingWeekMeal)
                {
                    var weekLabel = upcomingWeek == 1
                        ? "Week 1 (starting week)"
                        : $"Week {upcomingWeek} (the upcoming week)";

                    throw new ValidationException(
                        $"Meal plan for {weekLabel} cannot be empty. Please ensure meals are scheduled for the upcoming week.");
                }
            }
        }
    }

    private static TraineeMealPlanDto MapToDto(MealPlan plan)
    {
        var thumbnailUrl = plan.OrderDetails?.Package?.Media?
            .OrderBy(m => m.SortOrder)
            .Select(m => m.MediaUrl)
            .FirstOrDefault();

        var dto = new TraineeMealPlanDto
        {
            MealPlanId = plan.MealPlanId,
            OrderDetailsId = plan.OrderDetailsId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle
                        ?? plan.OrderDetails?.Package?.Title
                        ?? "Training Package",
            PackageThumbnailUrl = thumbnailUrl,
            CoachId = plan.Coach?.CoachId ?? plan.CoachId,
            CoachName = plan.Coach?.Coach?.FullName ?? "Your Coach",
            Title = plan.Title,
            Description = plan.Description,
            Status = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
        };

        var grouped = plan.MealPlanItems
            .GroupBy(i => i.WeekNumber ?? 1)
            .OrderBy(g => g.Key);

        foreach (var weekGroup in grouped)
        {
            var weekDto = new MealPlanWeekDto { WeekNumber = weekGroup.Key };

            var dayGroups = weekGroup
                .GroupBy(i => i.DayNumber ?? 1)
                .OrderBy(g => g.Key);

            foreach (var dayGroup in dayGroups)
            {
                var dayDto = new MealPlanDayDto { DayNumber = dayGroup.Key };
                dayDto.Items.AddRange(dayGroup.Select(i => new MealPlanItemDto
                {
                    MealPlanItemId = i.MealPlanItemId,
                    WeekNumber = i.WeekNumber,
                    DayNumber = i.DayNumber,
                    MealType = i.MealType,
                    FoodDescription = i.FoodDescription,
                    Calories = i.Calories,
                    SortOrder = i.SortOrder,
                }).OrderBy(i => i.SortOrder));

                weekDto.Days.Add(dayDto);
            }

            dto.Weeks.Add(weekDto);
        }

        return dto;
    }
}
