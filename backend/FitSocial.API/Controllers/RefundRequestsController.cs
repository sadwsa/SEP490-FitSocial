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
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
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
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<RefundRequestDto>>.Fail("Unauthorized"));
        }

        var result = await _refundRequestService.GetMyRefundRequestsAsync(currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin/Staff views all refund requests (optional status filter).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponseDto<List<RefundRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRefundRequests(
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _refundRequestService.GetAllRefundRequestsAsync(status, cancellationToken);
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
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var isAdminOrStaff = RoleConstants.IsAdminOrStaff(role);

        var result = await _refundRequestService.GetRefundRequestByIdAsync(id, currentUserId, isAdminOrStaff, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin/Staff processes (approves/rejects) a refund request.
    /// </summary>
    [HttpPut("{id:guid}/process")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResponseDto<RefundRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessRefundRequest(
        Guid id,
        [FromBody] ProcessRefundRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var staffUserId))
        {
            return Unauthorized(ApiResponseDto<RefundRequestDto>.Fail("Unauthorized"));
        }

        var result = await _refundRequestService.ProcessRefundRequestAsync(id, staffUserId, request, cancellationToken);
        return Ok(result);
    }
}
