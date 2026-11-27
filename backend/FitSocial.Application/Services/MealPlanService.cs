using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Helpers;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class MealPlanService : IMealPlanService
{
    public static DateTime GetBusinessToday() => PlanScheduleHelper.GetBusinessToday();

    private readonly IMealPlanRepository _mealPlanRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationRepository? _notifications;

    public MealPlanService(
        IMealPlanRepository mealPlanRepository,
        IUnitOfWork unitOfWork,
        INotificationRepository? notifications = null)
    {
        _mealPlanRepository = mealPlanRepository;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    private static void ValidateOrderStatusForTraineeAccess(Order? order)
        => PlanScheduleHelper.ValidateOrderStatusForTraineeAccess(order, "meal plan");

    public async Task<TraineeMealPlanDto?> GetTraineeMealPlanAsync(
        Guid traineeId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
            throw new ForbiddenException("Invalid trainee identity.");

        var plan = await _mealPlanRepository.GetMealPlanByOrderDetailsAsync(orderDetailsId, ct);
        if (plan == null)
        {
            var od = await _mealPlanRepository.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, ct);
            if (od?.Order != null)
            {
                if (od.Order.BuyerId != traineeId)
                {
                    throw new ForbiddenException("You do not have permission to view this meal plan.");
                }
                ValidateOrderStatusForTraineeAccess(od.Order);

                return new TraineeMealPlanDto
                {
                    MealPlanId = Guid.Empty,
                    OrderDetailsId = orderDetailsId,
                    PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Training Package",
                    PackageType = od.PackageType ?? od.Package?.PackageType ?? "BOTH",
                    CoachId = od.Order.CoachId ?? od.Package?.CoachId ?? Guid.Empty,
                    CoachName = od.CoachName ?? od.Package?.Coach?.Coach?.FullName ?? "Your Coach",
                    Status = "PENDING_COACH"
                };
            }
            return null;
        }

        var orderBuyerId = plan.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != traineeId)
        {
            throw new ForbiddenException("You do not have permission to view this meal plan.");
        }
        ValidateOrderStatusForTraineeAccess(plan.OrderDetails?.Order);

        var isDraftOrPending = string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(plan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase);
        if (isDraftOrPending)
        {
            var draftDto = MapToDto(plan, traineeId);
            draftDto.Weeks = new List<MealPlanWeekDto>();
            draftDto.TotalMealsCount = 0;
            draftDto.CompletedMealsCount = 0;
            draftDto.MissedMealsCount = 0;
            draftDto.AdherenceRate = 0;
            return draftDto;
        }

        await BackfillIncompleteLogsAsync(plan, traineeId, ct);
        return MapToDto(plan, traineeId);
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
            if ((string.Equals(dto.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(dto.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)) && !existingPlan.PublishedAt.HasValue)
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
                await _mealPlanRepository.AddItemAsync(newItem, ct);
                existingPlan.MealPlanItems.Add(newItem);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _mealPlanRepository.GetByIdWithItemsAsync(planId, asNoTracking: true, ct);
        return MapToDto(updated ?? existingPlan);
    }

    public static DateTime GetStartMonday(DateTime startDate)
        => PlanScheduleHelper.GetStartMonday(startDate);

    public static DateTime GetSessionDate(DateTime startDate, short weekNumber, short dayNumber)
        => PlanScheduleHelper.GetSessionDate(startDate, weekNumber, dayNumber);

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

    private async Task BackfillIncompleteLogsAsync(MealPlan plan, Guid traineeId, CancellationToken ct)
    {
        if (!plan.StartDate.HasValue || traineeId == Guid.Empty)
        {
            return;
        }

        var businessToday = GetBusinessToday();
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var created = false;

        foreach (var item in plan.MealPlanItems ?? new List<MealPlanItem>())
        {
            var scheduledDate = ResolveScheduledDate(plan.StartDate, plan.EndDate, item.WeekNumber ?? 1, item.DayNumber ?? 1);
            if (!scheduledDate.HasValue || scheduledDate.Value.Date >= businessToday)
            {
                continue;
            }

            var alreadyLogged = item.MealCompletionLogs?.Any(l => l.TraineeId == traineeId) == true;
            if (alreadyLogged)
            {
                continue;
            }

            var newLog = new MealCompletionLog
            {
                MealCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                MealPlanItemId = item.MealPlanItemId,
                LogDate = scheduledDate.Value,
                Status = "INCOMPLETE",
                CreatedAt = now
            };

            await _mealPlanRepository.AddMealLogAsync(newLog, ct);
            item.MealCompletionLogs ??= new List<MealCompletionLog>();
            item.MealCompletionLogs.Add(newLog);
            created = true;
        }

        if (created)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Concurrent backfill inserted logs for the same day/trainee; safe to continue.
            }
        }
    }

    public async Task<MealCompletionResultDto> RequestMealCompletionAsync(
        Guid traineeId,
        RequestMealCompletionDto dto,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
            throw new ForbiddenException("Invalid trainee identity.");

        if (dto == null || dto.MealPlanItemId == Guid.Empty)
            throw new ValidationException("MealPlanItemId is required.");

        if (string.IsNullOrWhiteSpace(dto.MediaUrl))
            throw new ValidationException("Please upload a photo of your meal before submitting for review.");

        var mediaUrl = dto.MediaUrl.Trim();
        if (mediaUrl.Length > 500)
            throw new ValidationException("MediaUrl cannot exceed 500 characters.");

        if (!string.IsNullOrWhiteSpace(dto.Notes) && dto.Notes.Length > 300)
            throw new ValidationException("Notes cannot exceed 300 characters.");

        var item = await _mealPlanRepository.GetMealItemByIdAsync(dto.MealPlanItemId, ct);
        if (item == null)
            throw new NotFoundException("Meal item not found.");

        var orderBuyerId = item.MealPlan?.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != traineeId)
            throw new ForbiddenException("You are not authorized to submit completion for this meal.");

        ValidateOrderStatusForTraineeAccess(item.MealPlan?.OrderDetails?.Order);

        var parentPlan = item.MealPlan;
        if (parentPlan != null &&
            (string.Equals(parentPlan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "DELETED", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("This meal plan is not active yet.");
        }

        var businessToday = GetBusinessToday();
        DateTime? scheduledDate = null;

        if (parentPlan != null && parentPlan.StartDate.HasValue)
        {
            scheduledDate = ResolveScheduledDate(parentPlan.StartDate, parentPlan.EndDate, item.WeekNumber ?? 1, item.DayNumber ?? 1);
            if (!scheduledDate.HasValue)
                throw new ValidationException("This meal is outside the scheduled meal plan dates.");

            if (scheduledDate.Value.Date != businessToday)
                throw new ValidationException($"You can only complete meals scheduled for today ({businessToday:dd/MM/yyyy}). This meal is scheduled for {scheduledDate.Value:dd/MM/yyyy}.");
        }

        var existingLog = await _mealPlanRepository.GetMealLogAsync(dto.MealPlanItemId, traineeId, ct);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        Guid logId;
        var savedNotes = dto.Notes?.Trim();

        if (existingLog != null)
        {
            if (string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                throw new BusinessException("This meal has already been completed and approved by coach.");

            existingLog.Status = "PENDING_REVIEW";
            existingLog.MediaUrl = mediaUrl;
            if (savedNotes != null) existingLog.Notes = savedNotes;
            existingLog.CoachFeedback = null;
            existingLog.ReviewedBy = null;
            existingLog.ReviewedAt = null;
            existingLog.CreatedAt = now;
            logId = existingLog.MealCompletionLogId;
        }
        else
        {
            var newLog = new MealCompletionLog
            {
                MealCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                MealPlanItemId = dto.MealPlanItemId,
                LogDate = scheduledDate ?? now,
                Status = "PENDING_REVIEW",
                MediaUrl = mediaUrl,
                Notes = savedNotes,
                CreatedAt = now
            };
            await _mealPlanRepository.AddMealLogAsync(newLog, ct);
            logId = newLog.MealCompletionLogId;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        if (_notifications != null && parentPlan != null && parentPlan.CoachId != Guid.Empty)
        {
            var traineeName = parentPlan.OrderDetails?.Order?.Buyer?.FullName ?? "Trainee";
            var packageTitle = parentPlan.OrderDetails?.PackageTitle ?? parentPlan.OrderDetails?.Package?.Title ?? parentPlan.Title ?? "Meal Package";
            var mealLabel = item.MealType ?? "Meal";

            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = parentPlan.CoachId,
                ActorId = traineeId,
                ReferenceId = parentPlan.MealPlanId,
                Type = "MEAL_COMPLETION_REQUESTED",
                Description = $"Trainee {traineeName} submitted a photo proof for {mealLabel} (Week {item.WeekNumber ?? 1}, Day {item.DayNumber ?? 1}) in package \"{packageTitle}\".",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        var refreshed = parentPlan?.OrderDetailsId != null
            ? await _mealPlanRepository.GetMealPlanByOrderDetailsAsync(parentPlan.OrderDetailsId.Value, ct)
            : null;
        var mapped = refreshed != null ? MapToDto(refreshed, traineeId) : null;

        return new MealCompletionResultDto
        {
            MealPlanItemId = dto.MealPlanItemId,
            IsCompleted = false,
            MealCompletionLogId = logId,
            CompletedAt = null,
            Status = "PENDING_REVIEW",
            Notes = savedNotes,
            MediaUrl = mediaUrl,
            TotalMealsCount = mapped?.TotalMealsCount ?? 0,
            CompletedMealsCount = mapped?.CompletedMealsCount ?? 0,
            ProgressPercentage = mapped?.ProgressPercentage ?? 0
        };
    }

    public async Task<MealCompletionResultDto> SaveMealMissedReasonAsync(
        Guid traineeId,
        SaveMealMissedReasonDto dto,
        CancellationToken ct = default)
    {
        if (traineeId == Guid.Empty)
            throw new ForbiddenException("Invalid trainee identity.");

        if (dto == null || dto.MealPlanItemId == Guid.Empty)
            throw new ValidationException("MealPlanItemId is required.");

        if (string.IsNullOrWhiteSpace(dto.Notes))
            throw new ValidationException("Please enter the reason for missing this meal.");

        if (dto.Notes.Trim().Length > 300)
            throw new ValidationException("Notes cannot exceed 300 characters.");

        var item = await _mealPlanRepository.GetMealItemByIdAsync(dto.MealPlanItemId, ct);
        if (item == null)
            throw new NotFoundException("Meal item not found.");

        var orderBuyerId = item.MealPlan?.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != traineeId)
            throw new ForbiddenException("You are not authorized to update this meal.");

        ValidateOrderStatusForTraineeAccess(item.MealPlan?.OrderDetails?.Order);

        var parentPlan = item.MealPlan;
        if (parentPlan != null &&
            (string.Equals(parentPlan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase)
             || string.Equals(parentPlan.Status, "DELETED", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException("This meal plan is not active yet.");
        }

        DateTime? scheduledDate = null;
        if (parentPlan != null && parentPlan.StartDate.HasValue)
        {
            scheduledDate = ResolveScheduledDate(parentPlan.StartDate, parentPlan.EndDate, item.WeekNumber ?? 1, item.DayNumber ?? 1);
            if (!scheduledDate.HasValue || scheduledDate.Value.Date >= GetBusinessToday())
                throw new ValidationException("You can only add a missed reason for past scheduled meals.");
        }

        var existingLog = await _mealPlanRepository.GetMealLogAsync(dto.MealPlanItemId, traineeId, ct);
        if (existingLog != null && string.Equals(existingLog.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Completed meals cannot be marked incomplete.");

        if (existingLog != null && string.Equals(existingLog.Status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Meals pending coach review cannot be marked incomplete.");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var trimmedNote = dto.Notes.Trim();

        if (existingLog != null)
        {
            existingLog.Status = "INCOMPLETE";
            existingLog.Notes = trimmedNote;
        }
        else
        {
            existingLog = new MealCompletionLog
            {
                MealCompletionLogId = Guid.NewGuid(),
                TraineeId = traineeId,
                MealPlanItemId = dto.MealPlanItemId,
                LogDate = scheduledDate ?? now,
                Status = "INCOMPLETE",
                Notes = trimmedNote,
                CreatedAt = now
            };
            await _mealPlanRepository.AddMealLogAsync(existingLog, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new MealCompletionResultDto
        {
            MealPlanItemId = dto.MealPlanItemId,
            IsCompleted = false,
            MealCompletionLogId = existingLog.MealCompletionLogId,
            Status = "INCOMPLETE",
            Notes = existingLog.Notes
        };
    }

    public async Task<MealCompletionResultDto> ReviewMealCompletionAsync(
        Guid coachId,
        CoachReviewMealDto dto,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
            throw new ForbiddenException("Invalid coach identity.");

        if (dto == null || dto.MealPlanItemId == Guid.Empty)
            throw new ValidationException("MealPlanItemId is required.");

        if (dto.TraineeId == Guid.Empty)
            throw new ValidationException("TraineeId is required.");

        var normalizedDecision = dto.Decision?.Trim().ToUpperInvariant();
        if (normalizedDecision != "COMPLETED" && normalizedDecision != "UNCOMPLETE")
            throw new ValidationException("Decision must be either 'COMPLETED' or 'UNCOMPLETE'.");

        var feedback = dto.Feedback?.Trim();
        if (normalizedDecision == "UNCOMPLETE" && string.IsNullOrWhiteSpace(feedback))
            throw new ValidationException("Feedback is required when marking a meal as uncomplete so the trainee knows what to improve.");

        if (!string.IsNullOrWhiteSpace(feedback) && feedback.Length > 500)
            throw new ValidationException("Feedback cannot exceed 500 characters.");

        var item = await _mealPlanRepository.GetMealItemByIdAsync(dto.MealPlanItemId, ct);
        if (item == null)
            throw new NotFoundException("Meal item not found.");

        var parentPlan = item.MealPlan;
        var isOwner = parentPlan != null && (
            parentPlan.CoachId == coachId ||
            parentPlan.OrderDetails?.Order?.CoachId == coachId ||
            parentPlan.OrderDetails?.Package?.CoachId == coachId);
        if (!isOwner || parentPlan == null)
            throw new ForbiddenException("You are not authorized to review meals for this meal plan.");

        var orderBuyerId = parentPlan.OrderDetails?.Order?.BuyerId;
        if (!orderBuyerId.HasValue || orderBuyerId.Value != dto.TraineeId)
            throw new ForbiddenException("You are not authorized to review meals for this trainee.");

        ValidateOrderStatusForTraineeAccess(parentPlan.OrderDetails?.Order);

        var existingLog = await _mealPlanRepository.GetMealLogAsync(dto.MealPlanItemId, dto.TraineeId, ct);
        if (existingLog == null || string.Equals(existingLog.Status, "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Trainee has not submitted a completion request for this meal yet.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        existingLog.Status = normalizedDecision;
        existingLog.CoachFeedback = feedback;
        existingLog.ReviewedBy = coachId;
        existingLog.ReviewedAt = now;

        await _unitOfWork.SaveChangesAsync(ct);

        if (_notifications != null)
        {
            var coachName = parentPlan.Coach?.Coach?.FullName ?? "Coach";
            var mealLabel = item.MealType ?? "Meal";
            var isApproved = normalizedDecision == "COMPLETED";

            var notifDesc = isApproved
                ? (string.IsNullOrWhiteSpace(feedback)
                    ? $"Coach {coachName} approved your meal \"{mealLabel}\"."
                    : $"Coach {coachName} approved your meal \"{mealLabel}\". Feedback: \"{feedback}\"")
                : $"Coach {coachName} reviewed your meal \"{mealLabel}\" as incomplete. Feedback: \"{feedback}\"";

            await _notifications.AddAsync(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = dto.TraineeId,
                ActorId = coachId,
                ReferenceId = parentPlan.MealPlanId,
                Type = isApproved ? "MEAL_APPROVED" : "MEAL_REJECTED",
                Description = notifDesc,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }, ct);

            await _unitOfWork.SaveChangesAsync(ct);
        }

        var refreshed = parentPlan.OrderDetailsId != null
            ? await _mealPlanRepository.GetMealPlanByOrderDetailsAsync(parentPlan.OrderDetailsId.Value, ct)
            : null;
        var mapped = refreshed != null ? MapToDto(refreshed, dto.TraineeId) : null;

        return new MealCompletionResultDto
        {
            MealPlanItemId = dto.MealPlanItemId,
            IsCompleted = normalizedDecision == "COMPLETED",
            MealCompletionLogId = existingLog.MealCompletionLogId,
            CompletedAt = normalizedDecision == "COMPLETED" ? existingLog.CreatedAt : null,
            Status = normalizedDecision,
            Notes = existingLog.Notes,
            MediaUrl = existingLog.MediaUrl,
            CoachFeedback = existingLog.CoachFeedback,
            ReviewedAt = existingLog.ReviewedAt,
            TotalMealsCount = mapped?.TotalMealsCount ?? 0,
            CompletedMealsCount = mapped?.CompletedMealsCount ?? 0,
            ProgressPercentage = mapped?.ProgressPercentage ?? 0
        };
    }

    public async Task<CoachTraineeMealProgressDto> GetCoachTraineeMealProgressAsync(
        Guid coachId,
        Guid orderDetailsId,
        CancellationToken ct = default)
    {
        if (coachId == Guid.Empty)
            throw new ForbiddenException("Invalid coach identity.");

        var plan = await _mealPlanRepository.GetCoachMealPlanWithLogsAsync(orderDetailsId, coachId, ct);
        if (plan == null)
        {
            var od = await _mealPlanRepository.GetOrderDetailWithOrderAndPackageAsync(orderDetailsId, ct);
            var isOwner = od != null && (
                (od.Order != null && od.Order.CoachId == coachId) ||
                (od.Package != null && od.Package.CoachId == coachId));

            if (!isOwner || od == null)
                throw new NotFoundException("Order detail not found or does not belong to you.");

            ValidateOrderStatusForTraineeAccess(od.Order);

            return new CoachTraineeMealProgressDto
            {
                OrderDetailsId = orderDetailsId,
                TraineeId = od.Order?.BuyerId ?? Guid.Empty,
                TraineeName = od.Order?.Buyer?.FullName ?? "Trainee",
                TraineeEmail = od.Order?.Buyer?.Email ?? string.Empty,
                PackageId = od.PackageId ?? Guid.Empty,
                PackageTitle = od.PackageTitle ?? od.Package?.Title ?? "Meal Package",
                PlanTitle = "No Meal Plan Created",
                PlanStatus = "DRAFT"
            };
        }

        ValidateOrderStatusForTraineeAccess(plan.OrderDetails?.Order);

        var buyer = plan.OrderDetails?.Order?.Buyer;
        var buyerUserId = buyer?.UserId ?? plan.OrderDetails?.Order?.BuyerId;

        var isDraftOrPending = string.Equals(plan.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(plan.Status, "PENDING_COACH", StringComparison.OrdinalIgnoreCase);
        if (!isDraftOrPending && buyerUserId.HasValue)
        {
            await BackfillIncompleteLogsAsync(plan, buyerUserId.Value, ct);
        }

        var weeks = BuildWeeksBreakdown(plan.MealPlanItems, buyerUserId, plan.StartDate, plan.EndDate, dropOutOfRange: true);

        var allLogs = plan.MealPlanItems
            .SelectMany(i => (i.MealCompletionLogs ?? new List<MealCompletionLog>())
                .Where(l => !buyerUserId.HasValue || l.TraineeId == buyerUserId.Value)
                .Select(l => new { Item = i, Log = l }))
            .OrderByDescending(x => x.Log.CreatedAt)
            .Take(50)
            .Select(x => new CoachMealActivityLogDto
            {
                LogId = x.Log.MealCompletionLogId,
                MealPlanItemId = x.Item.MealPlanItemId,
                MealType = x.Item.MealType ?? "Meal",
                FoodDescription = x.Item.FoodDescription ?? string.Empty,
                WeekNumber = x.Item.WeekNumber,
                DayNumber = x.Item.DayNumber,
                CompletedAt = x.Log.CreatedAt ?? DateTime.UtcNow,
                Status = x.Log.Status,
                Notes = x.Log.Notes,
                MediaUrl = x.Log.MediaUrl,
                CoachFeedback = x.Log.CoachFeedback,
                ReviewedAt = x.Log.ReviewedAt
            })
            .ToList();

        int totalCount = weeks.Sum(w => w.TotalMeals);
        int completedCount = weeks.Sum(w => w.CompletedMeals);
        int missedCount = weeks.Sum(w => w.MissedMeals);
        double adherenceRate = PlanScheduleHelper.CalculateAdherenceRate(completedCount, missedCount, totalCount);

        return new CoachTraineeMealProgressDto
        {
            OrderDetailsId = orderDetailsId,
            TraineeId = buyerUserId ?? Guid.Empty,
            TraineeName = buyer?.FullName ?? "Trainee",
            TraineeEmail = buyer?.Email ?? string.Empty,
            TraineeAvatarUrl = buyer?.AvatarUrl,
            PackageId = plan.OrderDetails?.PackageId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle ?? plan.OrderDetails?.Package?.Title ?? "Meal Package",
            MealPlanId = plan.MealPlanId,
            PlanTitle = plan.Title,
            PlanStatus = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            TotalMealsCount = totalCount,
            CompletedMealsCount = completedCount,
            MissedMealsCount = missedCount,
            AdherenceRate = adherenceRate,
            Weeks = weeks,
            RecentLogs = allLogs
        };
    }

    private static TraineeMealPlanDto MapToDto(MealPlan plan, Guid? filterTraineeId = null)
    {
        var thumbnailUrl = plan.OrderDetails?.Package?.Media?
            .OrderBy(m => m.SortOrder)
            .Select(m => m.MediaUrl)
            .FirstOrDefault();

        var weeks = BuildWeeksBreakdown(
            plan.MealPlanItems,
            filterTraineeId,
            plan.StartDate,
            plan.EndDate,
            dropOutOfRange: filterTraineeId.HasValue);

        int totalCount = weeks.Sum(w => w.TotalMeals);
        int completedCount = weeks.Sum(w => w.CompletedMeals);
        int missedCount = weeks.Sum(w => w.MissedMeals);
        double adherenceRate = PlanScheduleHelper.CalculateAdherenceRate(completedCount, missedCount, totalCount);

        return new TraineeMealPlanDto
        {
            MealPlanId = plan.MealPlanId,
            OrderDetailsId = plan.OrderDetailsId ?? Guid.Empty,
            PackageTitle = plan.OrderDetails?.PackageTitle
                        ?? plan.OrderDetails?.Package?.Title
                        ?? "Training Package",
            PackageThumbnailUrl = thumbnailUrl,
            PackageType = plan.OrderDetails?.PackageType
                       ?? plan.OrderDetails?.Package?.PackageType
                       ?? "BOTH",
            CoachId = plan.Coach?.CoachId ?? plan.CoachId,
            CoachName = plan.Coach?.Coach?.FullName ?? "Your Coach",
            Title = plan.Title,
            Description = plan.Description,
            Status = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
            TotalMealsCount = totalCount,
            CompletedMealsCount = completedCount,
            MissedMealsCount = missedCount,
            AdherenceRate = adherenceRate,
            Weeks = weeks
        };
    }

    private static List<MealPlanWeekDto> BuildWeeksBreakdown(
        IEnumerable<MealPlanItem> items,
        Guid? filterTraineeId,
        DateTime? planStartDate,
        DateTime? planEndDate,
        bool dropOutOfRange)
    {
        var businessToday = GetBusinessToday();
        var weeks = new List<MealPlanWeekDto>();

        var grouped = items
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
                var scheduledDate = ResolveScheduledDate(planStartDate, planEndDate, weekGroup.Key, dayGroup.Key);
                var hasSchedule = planStartDate.HasValue;

                if (dropOutOfRange && hasSchedule && !scheduledDate.HasValue)
                {
                    continue;
                }

                var dayDto = new MealPlanDayDto
                {
                    DayNumber = dayGroup.Key,
                    ScheduledDate = scheduledDate,
                    IsToday = !hasSchedule || (scheduledDate.HasValue && scheduledDate.Value.Date == businessToday)
                };

                foreach (var i in dayGroup.OrderBy(x => x.SortOrder))
                {
                    MealCompletionLog? relevantLog = null;
                    if (i.MealCompletionLogs != null && i.MealCompletionLogs.Count > 0)
                    {
                        var mine = i.MealCompletionLogs
                            .Where(l => !filterTraineeId.HasValue || l.TraineeId == filterTraineeId.Value)
                            .ToList();

                        relevantLog = mine.FirstOrDefault(l => string.Equals(l.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                                      ?? mine.OrderByDescending(l => l.CreatedAt).FirstOrDefault();
                    }

                    var isCompleted = string.Equals(relevantLog?.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase);

                    dayDto.Items.Add(new MealPlanItemDto
                    {
                        MealPlanItemId = i.MealPlanItemId,
                        WeekNumber = i.WeekNumber,
                        DayNumber = i.DayNumber,
                        MealType = i.MealType,
                        FoodDescription = i.FoodDescription,
                        Calories = i.Calories,
                        SortOrder = i.SortOrder,
                        IsCompleted = isCompleted,
                        MealCompletionLogId = relevantLog?.MealCompletionLogId,
                        CompletedAt = relevantLog?.CreatedAt,
                        TraineeNotes = relevantLog?.Notes,
                        MediaUrl = relevantLog?.MediaUrl,
                        CoachFeedback = relevantLog?.CoachFeedback,
                        ReviewedAt = relevantLog?.ReviewedAt,
                        Status = relevantLog?.Status,
                        ScheduledDate = scheduledDate
                    });
                }

                if (dayDto.Items.Count > 0)
                {
                    weekDto.Days.Add(dayDto);
                }
            }

            if (weekDto.Days.Count > 0)
            {
                weeks.Add(weekDto);
            }
        }

        return weeks;
    }

    private static DateTime? ResolveScheduledDate(DateTime? planStartDate, DateTime? planEndDate, short weekNumber, short dayNumber)
        => PlanScheduleHelper.ResolveScheduledDate(planStartDate, planEndDate, weekNumber, dayNumber);
}
