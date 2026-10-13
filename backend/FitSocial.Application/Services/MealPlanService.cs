using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class MealPlanService : IMealPlanService
{
    private readonly IMealPlanRepository _mealPlanRepository;

    public MealPlanService(IMealPlanRepository mealPlanRepository)
    {
        _mealPlanRepository = mealPlanRepository;
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

        // Get package thumbnail from media
        var thumbnailUrl = plan.OrderDetails?.Package?.Media?
            .OrderBy(m => m.SortOrder)
            .Select(m => m.MediaUrl)
            .FirstOrDefault();

        var dto = new TraineeMealPlanDto
        {
            MealPlanId = plan.MealPlanId,
            OrderDetailsId = orderDetailsId,
            PackageTitle = plan.OrderDetails?.PackageTitle
                        ?? plan.OrderDetails?.Package?.Title
                        ?? "Training Package",
            PackageThumbnailUrl = thumbnailUrl,
            CoachName = plan.Coach?.Coach?.FullName ?? "Your Coach",
            Title = plan.Title,
            Description = plan.Description,
            Status = plan.Status,
            StartDate = plan.StartDate,
            EndDate = plan.EndDate,
        };

        // Group items by Week → Day
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
