using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Get a paginated list of reports for Staff/Admin with optional filtering by status, target type, type, appealStatus, reporter, and date range.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<ReportListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports([FromQuery] GetReportsQueryDto query, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetReportsAsync(query, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get details of a single report including evidence media and appeal info.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetReportByIdAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Resolve or dismiss a report (Admin/Staff).
    /// </summary>
    [HttpPut("{id:guid}/resolve")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResolveReport(Guid id, [FromBody] ResolveReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var staffId))
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ResolveReportAsync(id, staffId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submit an appeal against a report (by reported user).
    /// </summary>
    [HttpPost("{id:guid}/appeal")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAppeal(Guid id, [FromBody] SubmitAppealRequestDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.SubmitAppealAsync(id, currentUserId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Review an appeal against a report (Admin/Staff).
    /// </summary>
    [HttpPut("{id:guid}/appeal")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewAppeal(Guid id, [FromBody] ReviewAppealRequestDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var staffId))
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ReviewAppealAsync(id, staffId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Report a user for violating rules.
    /// </summary>
    [HttpPost("user")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportUser([FromBody] CreateUserReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ReportUserAsync(currentUserId, request, cancellationToken);
        return Ok(result);
    }
}
