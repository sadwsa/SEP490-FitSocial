using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Refunds;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RefundRequestsController : ControllerBase
{
    private readonly IRefundRequestService _refundRequestService;

    public RefundRequestsController(IRefundRequestService refundRequestService)
    {
        _refundRequestService = refundRequestService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out userId);
    }

    /// <summary>
    /// User submits a refund request for an order.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRefundRequest(
        [FromBody] CreateRefundRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var result = await _refundRequestService.CreateRefundRequestAsync(currentUserId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// User views their submitted refund requests.
    /// </summary>
    [HttpGet("my-requests")]
    [ProducesResponseType(typeof(ApiResponseDto<List<RefundRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyRefundRequests(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<RefundRequestDto>>.Fail("Unauthorized"));
        }

        var result = await _refundRequestService.GetMyRefundRequestsAsync(currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin/Staff views paged and filtered refund requests list.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<RefundRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRefundRequests(
        [FromQuery] GetRefundRequestsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _refundRequestService.GetPagedRefundRequestsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a specific refund request.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefundRequestById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var isAdminOrStaff = RoleConstants.IsAdminOrStaff(role);

        var result = await _refundRequestService.GetRefundRequestByIdAsync(id, currentUserId, isAdminOrStaff, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin/Staff approves a refund request.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveRefundRequest(
        Guid id,
        [FromBody] ApproveRefundRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var staffUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _refundRequestService.ApproveRefundRequestAsync(
            id, staffUserId, request ?? new ApproveRefundRequestDto(), ipAddress, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin/Staff rejects a refund request with a note.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectRefundRequest(
        Guid id,
        [FromBody] RejectRefundRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var staffUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _refundRequestService.RejectRefundRequestAsync(
            id, staffUserId, request, ipAddress, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Legacy process endpoint for backwards compatibility.
    /// </summary>
    [HttpPut("{id:guid}/process")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessRefundRequest(
        Guid id,
        [FromBody] ProcessRefundRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var staffUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var result = await _refundRequestService.ProcessRefundRequestAsync(id, staffUserId, request, cancellationToken);
        return Ok(result);
    }
}
