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

[ApiController]
[Route("api/trainee/meal-plan")]
[Authorize]
public class TraineeMealPlanController : ControllerBase
{
    private readonly IMealPlanService _mealPlanService;
    private readonly ILogger<TraineeMealPlanController> _logger;

    public TraineeMealPlanController(
        IMealPlanService mealPlanService,
        ILogger<TraineeMealPlanController> logger)
    {
        _mealPlanService = mealPlanService;
        _logger = logger;
    }

    /// <summary>
    /// Get the meal plan assigned to a trainee for a specific order detail.
    /// </summary>
    [HttpGet("{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeMealPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMealPlan(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
            return Unauthorized(ApiResponseDto<TraineeMealPlanDto>.Fail("Unauthorized: Invalid user identity."));

        var result = await _mealPlanService.GetTraineeMealPlanAsync(traineeId, orderDetailsId, cancellationToken);
        if (result == null)
            return Ok(ApiResponseDto<TraineeMealPlanDto?>.Ok(null, "No meal plan assigned yet."));

        return Ok(ApiResponseDto<TraineeMealPlanDto>.Ok(result, "Meal plan retrieved successfully."));
    }

    /// <summary>
    /// Request Meal Completion: Trainee uploads meal photo and notes for Coach to review.
    /// </summary>
    [HttpPost("request-completion")]
    [ProducesResponseType(typeof(ApiResponseDto<MealCompletionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestMealCompletion(
        [FromBody] RequestMealCompletionDto dto,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
            return Unauthorized(ApiResponseDto<MealCompletionResultDto>.Fail("Unauthorized: Invalid user identity."));

        var result = await _mealPlanService.RequestMealCompletionAsync(traineeId, dto, cancellationToken);
        return Ok(ApiResponseDto<MealCompletionResultDto>.Ok(result, "Meal completion proof submitted! Waiting for coach review."));
    }

    /// <summary>
    /// Save Missed Meal Reason: Allows trainee to record a reason for a past missed meal.
    /// </summary>
    [HttpPost("missed-reason")]
    [ProducesResponseType(typeof(ApiResponseDto<MealCompletionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveMealMissedReason(
        [FromBody] SaveMealMissedReasonDto dto,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
            return Unauthorized(ApiResponseDto<MealCompletionResultDto>.Fail("Unauthorized: Invalid user identity."));

        var result = await _mealPlanService.SaveMealMissedReasonAsync(traineeId, dto, cancellationToken);
        return Ok(ApiResponseDto<MealCompletionResultDto>.Ok(result, "Missed meal reason saved successfully."));
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : Guid.Empty;
    }
}
