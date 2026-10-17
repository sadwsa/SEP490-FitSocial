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

        try
        {
            var result = await _mealPlanService.GetTraineeMealPlanAsync(traineeId, orderDetailsId, cancellationToken);
            if (result == null)
                return Ok(ApiResponseDto<TraineeMealPlanDto?>.Ok(null, "No meal plan assigned yet."));

            return Ok(ApiResponseDto<TraineeMealPlanDto>.Ok(result, "Meal plan retrieved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MealPlan] Error getting meal plan for order {OrderDetailsId}: {Message}", orderDetailsId, ex.Message);
            throw;
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : Guid.Empty;
    }
}
