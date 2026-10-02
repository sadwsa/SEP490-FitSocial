using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// Controller for UC_17: View Coach Dashboard.
/// Serves overview metrics for the Coach Navigation Sidebar ("Dashboard & Revenue / Orders").
/// </summary>
[ApiController]
[Route("api/coach/dashboard")]
[Authorize(Roles = "COACH")]
public class CoachDashboardController : ControllerBase
{
    private readonly ICoachDashboardService _dashboardService;

    public CoachDashboardController(ICoachDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Gets performance overview metrics for the authenticated coach.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dashboard overview metrics including active trainees, packages sold, monthly revenue, and pending requests.</returns>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachDashboardMetricsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboardOverview(CancellationToken cancellationToken = default)
    {
        // Extract coach ID directly from validated JWT claim to prevent Insecure Direct Object References (IDOR)
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var coachId))
        {
            return Unauthorized(ApiResponseDto<CoachDashboardMetricsDto>.Fail("Unauthorized: Invalid user identity in token."));
        }

        var result = await _dashboardService.GetCoachDashboardMetricsAsync(coachId, cancellationToken);
        return Ok(result);
    }
}
