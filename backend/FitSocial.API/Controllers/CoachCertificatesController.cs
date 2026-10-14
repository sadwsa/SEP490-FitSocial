using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/admin/coach-certificates")]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
public class CoachCertificatesController : ControllerBase
{
    private readonly ICoachCertificateService _certificateService;

    public CoachCertificatesController(ICoachCertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out userId);
    }

    /// <summary>
    /// UC: View Coach Certificate List with pagination, status filtering, and search.
    /// Accessible to Staff and Admin.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<CoachCertificateAdminListResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCertificates(
        [FromQuery] GetCoachCertificatesAdminQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _certificateService.GetCertificatesAsync(query ?? new GetCoachCertificatesAdminQueryDto(), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC: View Coach Certificate Details.
    /// Accessible to Staff and Admin.
    /// </summary>
    [HttpGet("{certificateId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachCertificateAdminDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCertificateById(
        Guid certificateId,
        CancellationToken cancellationToken = default)
    {
        var result = await _certificateService.GetCertificateByIdAsync(certificateId, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC: Approve a pending Coach Certificate.
    /// Accessible to Staff and Admin.
    /// </summary>
    [HttpPut("{certificateId:guid}/approve")]
    [HttpPost("{certificateId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachCertificateAdminDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveCertificate(
        Guid certificateId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Unauthorized"));
        }

        var result = await _certificateService.ApproveCertificateAsync(certificateId, currentUserId, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            if (result.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC: Reject a pending Coach Certificate with a required reason.
    /// Accessible to Staff and Admin.
    /// </summary>
    [HttpPut("{certificateId:guid}/reject")]
    [HttpPost("{certificateId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachCertificateAdminDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectCertificate(
        Guid certificateId,
        [FromBody] RejectCoachCertificateDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Unauthorized"));
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(ApiResponseDto<CoachCertificateAdminDetailDto>.Fail("Rejection reason is required and cannot be empty."));
        }

        var result = await _certificateService.RejectCertificateAsync(certificateId, currentUserId, request, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }
            if (result.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }
}
