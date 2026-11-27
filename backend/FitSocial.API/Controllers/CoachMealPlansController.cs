using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.MealPlan;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// Controller for Coach Meal Plan management:
/// Upload Meal Plan (Create new nutrition/meal plan for trainee)
/// Update Meal Plan (Update existing meal plan)
/// </summary>
[ApiController]
[Route("api/coach/meal-plans")]
[Authorize(Roles = "COACH")]
public class CoachMealPlansController : ControllerBase
{
    private readonly IMealPlanService _mealPlanService;
    private readonly ILogger<CoachMealPlansController> _logger;

    public CoachMealPlansController(
        IMealPlanService mealPlanService,
        ILogger<CoachMealPlansController> logger)
    {
        _mealPlanService = mealPlanService;
        _logger = logger;
    }

    /// <summary>
    /// Creates/Uploads a new meal plan for an order detail belonging to the coach.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeMealPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateMealPlan(
        [FromBody] CreateMealPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TraineeMealPlanDto>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        var result = await _mealPlanService.CreateMealPlanAsync(coachId, dto, cancellationToken);
        return Ok(ApiResponseDto<TraineeMealPlanDto>.Ok(result, "Meal plan created successfully."));
    }

    /// <summary>
    /// Updates an existing meal plan and its meal items.
    /// </summary>
    [HttpPut("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeMealPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMealPlan(
        [FromRoute] Guid planId,
        [FromBody] UpdateMealPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TraineeMealPlanDto>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        var result = await _mealPlanService.UpdateMealPlanAsync(coachId, planId, dto, cancellationToken);
        return Ok(ApiResponseDto<TraineeMealPlanDto>.Ok(result, "Meal plan updated successfully."));
    }

    /// <summary>
    /// Gets the meal plan assigned to a specific order detail.
    /// </summary>
    [HttpGet("order-detail/{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeMealPlanDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMealPlanByOrderDetail(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TraineeMealPlanDto?>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        var result = await _mealPlanService.GetCoachMealPlanByOrderDetailsAsync(coachId, orderDetailsId, cancellationToken);
        return Ok(ApiResponseDto<TraineeMealPlanDto?>.Ok(result, "Meal plan retrieved successfully."));
    }

    /// <summary>
    /// Monitor Trainee Meal Progress: Retrieves trainee's meal adherence, meal photo proofs, and review history.
    /// </summary>
    [HttpGet("trainee-progress/{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachTraineeMealProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachTraineeMealProgress(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
            return Unauthorized(ApiResponseDto<CoachTraineeMealProgressDto>.Fail("Unauthorized: Invalid coach identity in token."));

        var result = await _mealPlanService.GetCoachTraineeMealProgressAsync(coachId, orderDetailsId, cancellationToken);
        return Ok(ApiResponseDto<CoachTraineeMealProgressDto>.Ok(result, "Trainee meal progress retrieved successfully."));
    }

    /// <summary>
    /// Review Meal Completion: Coach approves (COMPLETED) or rejects (UNCOMPLETE) a trainee's meal proof with feedback.
    /// </summary>
    [HttpPost("review-meal")]
    [ProducesResponseType(typeof(ApiResponseDto<MealCompletionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewMeal(
        [FromBody] CoachReviewMealDto dto,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
            return Unauthorized(ApiResponseDto<MealCompletionResultDto>.Fail("Unauthorized: Invalid coach identity in token."));

        var result = await _mealPlanService.ReviewMealCompletionAsync(coachId, dto, cancellationToken);
        return Ok(ApiResponseDto<MealCompletionResultDto>.Ok(result, "Meal review submitted successfully."));
    }

    private Guid GetCurrentCoachId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : Guid.Empty;
    }
}
