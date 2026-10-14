using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPlan;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// Controller for Coach Training Plan management:
/// UC_14.1.3: Coach Upload Training Plan (Create new training plan for trainee)
/// UC_14.1.4: Coach Update Training Plan (Update existing training plan)
/// </summary>
[ApiController]
[Route("api/coach/training-plans")]
[Authorize(Roles = "COACH")]
public class CoachTrainingPlansController : ControllerBase
{
    private readonly ITrainingPlanService _trainingPlanService;
    private readonly ILogger<CoachTrainingPlansController> _logger;

    public CoachTrainingPlansController(
        ITrainingPlanService trainingPlanService,
        ILogger<CoachTrainingPlansController> logger)
    {
        _trainingPlanService = trainingPlanService;
        _logger = logger;
    }

    /// <summary>
    /// UC_14.1.3: Creates/Uploads a new training plan for an order detail belonging to the coach.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<TrainingPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTrainingPlan(
        [FromBody] CreateTrainingPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TrainingPlanDetailDto>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        try
        {
            var result = await _trainingPlanService.CreateTrainingPlanAsync(coachId, dto, cancellationToken);
            return Ok(ApiResponseDto<TrainingPlanDetailDto>.Ok(result, "Training plan created successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoachTrainingPlans] Error creating training plan for coach {CoachId}: {Message}", coachId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// UC_14.1.4: Updates an existing training plan and its exercises.
    /// </summary>
    [HttpPut("{planId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TrainingPlanDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTrainingPlan(
        [FromRoute] Guid planId,
        [FromBody] UpdateTrainingPlanDto dto,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TrainingPlanDetailDto>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        try
        {
            var result = await _trainingPlanService.UpdateTrainingPlanAsync(coachId, planId, dto, cancellationToken);
            return Ok(ApiResponseDto<TrainingPlanDetailDto>.Ok(result, "Training plan updated successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoachTrainingPlans] Error updating training plan {PlanId} for coach {CoachId}: {Message}", planId, coachId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the training plan assigned to a specific order detail.
    /// </summary>
    [HttpGet("order-detail/{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TrainingPlanDetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrainingPlanByOrderDetail(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TrainingPlanDetailDto?>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        try
        {
            var result = await _trainingPlanService.GetTrainingPlanByOrderDetailsIdAsync(coachId, orderDetailsId, cancellationToken);
            return Ok(ApiResponseDto<TrainingPlanDetailDto?>.Ok(result, "Training plan retrieved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoachTrainingPlans] Error getting training plan for order detail {OrderDetailsId}: {Message}", orderDetailsId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the list of video tutorials created by the current coach for selection in exercise forms.
    /// </summary>
    [HttpGet("video-tutorials")]
    [ProducesResponseType(typeof(ApiResponseDto<List<VideoTutorialDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCoachVideoTutorials(CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<List<VideoTutorialDto>>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        try
        {
            var result = await _trainingPlanService.GetCoachVideoTutorialsAsync(coachId, cancellationToken);
            return Ok(ApiResponseDto<List<VideoTutorialDto>>.Ok(result, "Video tutorials retrieved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoachTrainingPlans] Error getting video tutorials for coach {CoachId}: {Message}", coachId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Monitor Trainee Workout Progress: Retrieves completion stats and logs for a trainee's purchased package.
    /// </summary>
    [HttpGet("trainee-progress/{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachTraineeWorkoutProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachTraineeProgress(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var coachId = GetCurrentCoachId();
        if (coachId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<CoachTraineeWorkoutProgressDto>.Fail("Unauthorized: Invalid coach identity in token."));
        }

        try
        {
            var result = await _trainingPlanService.GetCoachTraineeProgressAsync(coachId, orderDetailsId, cancellationToken);
            return Ok(ApiResponseDto<CoachTraineeWorkoutProgressDto>.Ok(result, "Trainee workout progress retrieved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CoachTrainingPlans] Error getting progress for order detail {OrderDetailsId}: {Message}", orderDetailsId, ex.Message);
            throw;
        }
    }

    private Guid GetCurrentCoachId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : Guid.Empty;
    }
}
