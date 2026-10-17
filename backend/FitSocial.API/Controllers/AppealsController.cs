using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Appeals;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppealsController : ControllerBase
{
    private readonly IAppealService _appealService;

    public AppealsController(IAppealService appealService)
    {
        _appealService = appealService;
    }

    /// <summary>
    /// Submit an appeal against a report (supports authenticated users & blocked users via AppealToken).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<AppealDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitAppeal([FromBody] SubmitAppealRequestDto request, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();

        // If request didn't include AppealToken in body, check header
        if (string.IsNullOrWhiteSpace(request.AppealToken))
        {
            request.AppealToken = Request.Headers["X-Appeal-Token"].ToString();
        }

        var result = await _appealService.SubmitAppealAsync(currentUserId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get violation reports eligible for appeal for the user (supports authenticated users & blocked users via AppealToken).
    /// </summary>
    [HttpGet("my-reports")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<AppealableReportDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppealableReports([FromQuery] string? appealToken = null, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var token = !string.IsNullOrWhiteSpace(appealToken) ? appealToken : Request.Headers["X-Appeal-Token"].ToString();

        var result = await _appealService.GetAppealableReportsAsync(currentUserId, token, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get details of a specific report to display on appeal form.
    /// </summary>
    [HttpGet("reports/{reportId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<AppealableReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportForAppeal(Guid reportId, [FromQuery] string? appealToken = null, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var token = !string.IsNullOrWhiteSpace(appealToken) ? appealToken : Request.Headers["X-Appeal-Token"].ToString();

        var result = await _appealService.GetReportForAppealAsync(reportId, currentUserId, token, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get appeal detail by ID.
    /// </summary>
    [HttpGet("{appealId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<AppealDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppealDetail(Guid appealId, [FromQuery] string? appealToken = null, CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var token = !string.IsNullOrWhiteSpace(appealToken) ? appealToken : Request.Headers["X-Appeal-Token"].ToString();

        var result = await _appealService.GetAppealDetailAsync(appealId, currentUserId, token, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(userIdStr, out var id))
        {
            return id;
        }

        return null;
    }
}
