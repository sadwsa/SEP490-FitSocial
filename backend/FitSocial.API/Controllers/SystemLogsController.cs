using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemLogs;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/admin/system-logs")]
[Authorize(Roles = RoleConstants.Admin)]
public class SystemLogsController : ControllerBase
{
    private readonly ISystemLogService _systemLogService;

    public SystemLogsController(ISystemLogService systemLogService)
    {
        _systemLogService = systemLogService;
    }

    /// <summary>
    /// UC_28 / UC_36: Get paginated administrative system logs with optional search, role, actionType, status, source, level, and date filters (ADMIN only).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<SystemLogItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSystemLogs(
        [FromQuery] GetSystemLogsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _systemLogService.GetSystemLogsAsync(query, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// UC_28 / UC_36: Get summary statistics for administrative system logs (Total, Admin, Staff, Failed) (ADMIN only).
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponseDto<SystemLogSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSystemLogsSummary(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _systemLogService.GetSystemLogsSummaryAsync(fromDate, toDate, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// UC_28 / UC_36: Get detailed information of a specific administrative system log entry (ADMIN only).
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponseDto<SystemLogItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLogDetail(
        long id,
        CancellationToken cancellationToken = default)
    {
        var result = await _systemLogService.GetLogDetailAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
