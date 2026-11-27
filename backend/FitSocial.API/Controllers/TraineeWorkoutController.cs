using System;
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

[ApiController]
[Route("api/trainee/workout")]
[Authorize]
public class TraineeWorkoutController : ControllerBase
{
    private readonly ITrainingPlanService _trainingPlanService;
    private readonly ILogger<TraineeWorkoutController> _logger;

    public TraineeWorkoutController(
        ITrainingPlanService trainingPlanService,
        ILogger<TraineeWorkoutController> logger)
    {
        _trainingPlanService = trainingPlanService;
        _logger = logger;
    }

    /// <summary>
    /// View Training Progress: Retrieves the training plan, exercises, tutorials, and completion status for a trainee's order detail.
    /// </summary>
    [HttpGet("plan/{orderDetailsId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeWorkoutPlanProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeWorkoutPlan(
        [FromRoute] Guid orderDetailsId,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TraineeWorkoutPlanProgressDto>.Fail("Unauthorized: Invalid user identity."));
        }

        var result = await _trainingPlanService.GetTraineeWorkoutPlanAsync(traineeId, orderDetailsId, cancellationToken);
        return Ok(ApiResponseDto<TraineeWorkoutPlanProgressDto>.Ok(result, "Workout plan retrieved successfully."));
    }

    /// <summary>
    /// View Training Progress: Retrieves the training plan by purchased package ID.
    /// </summary>
    [HttpGet("plan/by-package/{packageId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<TraineeWorkoutPlanProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTraineeWorkoutPlanByPackage(
        [FromRoute] Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<TraineeWorkoutPlanProgressDto>.Fail("Unauthorized: Invalid user identity."));
        }

        var result = await _trainingPlanService.GetTraineeWorkoutPlanByPackageAsync(traineeId, packageId, cancellationToken);
        return Ok(ApiResponseDto<TraineeWorkoutPlanProgressDto>.Ok(result, "Workout plan retrieved successfully."));
    }

    /// <summary>
    /// Mark Daily Workout: Marks an exercise as incomplete (missed reason), with optional trainee notes.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(ApiResponseDto<WorkoutCompletionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleWorkoutCompletion(
        [FromBody] ToggleWorkoutCompletionDto dto,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<WorkoutCompletionResultDto>.Fail("Unauthorized: Invalid user identity."));
        }

        if (!string.Equals(dto?.Status?.Trim(), "INCOMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponseDto<object>.Fail("Direct workout self-completion is disabled. Please submit completion proof for coach review."));
        }

        var result = await _trainingPlanService.ToggleWorkoutCompletionAsync(traineeId, dto!, cancellationToken);
        return Ok(ApiResponseDto<WorkoutCompletionResultDto>.Ok(result, "Workout completion updated successfully."));
    }

    /// <summary>
    /// Request Training Completion: Trainee submits proof photo/video and notes for Coach to review.
    /// </summary>
    [HttpPost("request-completion")]
    [ProducesResponseType(typeof(ApiResponseDto<WorkoutCompletionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestWorkoutCompletion(
        [FromBody] RequestWorkoutCompletionDto dto,
        CancellationToken cancellationToken = default)
    {
        var traineeId = GetCurrentUserId();
        if (traineeId == Guid.Empty)
        {
            return Unauthorized(ApiResponseDto<WorkoutCompletionResultDto>.Fail("Unauthorized: Invalid user identity."));
        }

        var result = await _trainingPlanService.RequestWorkoutCompletionAsync(traineeId, dto, cancellationToken);
        return Ok(ApiResponseDto<WorkoutCompletionResultDto>.Ok(result, "Workout completion request submitted successfully. Waiting for coach review."));
    }

    private Guid GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var id) ? id : Guid.Empty;
    }
}
