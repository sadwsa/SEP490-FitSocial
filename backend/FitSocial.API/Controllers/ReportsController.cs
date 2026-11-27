using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Services;
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
    private readonly CloudinaryImageService? _imageService;

    public ReportsController(IReportService reportService, CloudinaryImageService? imageService = null)
    {
        _reportService = reportService;
        _imageService = imageService;
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
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("User is not authenticated."));
        }

        var result = await _reportService.GetReportsAsync(query, currentUserId, cancellationToken);
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
    /// Process a violation report with an action: WARN, BLOCK, LOCK, REJECT, or DISMISS (Admin/Staff).
    /// </summary>
    [HttpPost("{id:guid}/process")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<ReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProcessReport(Guid id, [FromBody] ProcessViolationReportRequestDto request, CancellationToken cancellationToken = default)
    {
        var staffId = GetCurrentUserId();
        if (!staffId.HasValue)
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ProcessViolationReportAsync(id, staffId.Value, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

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
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.SubmitAppealAsync(id, currentUserId.Value, request, cancellationToken);
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
        var staffId = GetCurrentUserId();
        if (!staffId.HasValue)
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ReviewAppealAsync(id, staffId.Value, request, cancellationToken);
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
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponseDto<ReportListItemDto>.Fail("Unauthorized"));
        }

        var result = await _reportService.ReportUserAsync(currentUserId.Value, request, cancellationToken);
        return Ok(result);
    }

    // Upload ảnh bằng chứng cho báo cáo (trước khi gửi báo cáo)
    [HttpPost("upload-images")]
    public async Task<IActionResult> UploadReportImages(IFormFileCollection files, CancellationToken cancellationToken = default)
    {
        if (_imageService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponseDto<List<string>>.Fail("Image upload service is unavailable."));
        }

        if (files == null || files.Count == 0)
        {
            return BadRequest(ApiResponseDto<List<string>>.Fail("Please select at least one image."));
        }

        if (files.Count > 5)
        {
            return BadRequest(ApiResponseDto<List<string>>.Fail("You can upload a maximum of 5 images at once."));
        }

        var uploadedUrls = await _imageService.UploadImagesFromFilesAsync(files, cancellationToken);
        return Ok(ApiResponseDto<List<string>>.Ok(uploadedUrls, "Images uploaded successfully."));
    }

    private Guid? GetCurrentUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out var uId) && uId != Guid.Empty ? uId : null;
    }
}
