using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize] // Bảo mật route (Admin/Coach)
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
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachAnalytics(Guid coachId, CancellationToken cancellationToken = default)
    {
        // Có thể áp dụng validate Authorization ở đây nếu cần (chỉ Coach chính nó hoặc Admin mới có quyền xem)
        // Ví dụ: var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var result = await _dashboardService.GetCoachDashboardAnalyticsAsync(coachId, cancellationToken);
        return Ok(result);
    }
}
