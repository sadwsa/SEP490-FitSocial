using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = $"{RoleConstants.Coach},{RoleConstants.Admin},{RoleConstants.Staff}")]
public class AnalyticsController : ControllerBase
{
    private readonly ICoachDashboardService _dashboardService;

    public AnalyticsController(ICoachDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Lấy dữ liệu phân tích chi tiết của Coach.
    /// </summary>
    /// <param name="coachId">ID của coach cần phân tích.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("coach/{coachId}")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachAnalytics(Guid coachId, CancellationToken cancellationToken = default)
    {
        // Khóa chống IDOR: Chỉ Coach chính chủ hoặc Admin/Staff mới có quyền xem dữ liệu phân tích
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<object>.Fail("Unauthorized: Invalid user credentials."));
        }

        var isAuditAuthorized = User.IsInRole(RoleConstants.Admin) || User.IsInRole(RoleConstants.Staff);
        var isOwnCoach = User.IsInRole(RoleConstants.Coach) && currentUserId == coachId;
        if (!isAuditAuthorized && !isOwnCoach)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponseDto<object>.Fail("Access denied: You are only allowed to view your own analytics."));
        }

        var result = await _dashboardService.GetCoachDashboardAnalyticsAsync(coachId, cancellationToken);
        return Ok(result);
    }
}
