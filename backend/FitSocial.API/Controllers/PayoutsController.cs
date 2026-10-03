using System.Security.Claims;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payouts;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// UC-23.1 Process Coach Payout (Staff, Admin).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutsController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdStr, out userId);
    }

    /// <summary>
    /// Payout History management list with optional cycle/status/coach filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<PayoutHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] string? status,
        [FromQuery] Guid? coachId,
        CancellationToken cancellationToken = default)
    {
        var result = await _payoutService.GetPayoutHistoryAsync(month, year, status, coachId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Builds (or refreshes pending) payout snapshots for a settlement cycle.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponseDto<GeneratePayoutsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate([FromBody] GeneratePayoutsRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest(ApiResponseDto<GeneratePayoutsResultDto>.Fail("Month and year are required."));
        }

        var result = await _payoutService.GenerateMonthlyPayoutsAsync(request.Month, request.Year, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Payout processing modal data: breakdown, bank account, report/bank guards.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<PayoutDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _payoutService.GetPayoutDetailAsync(id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Confirm payout execution: transfers via banking gateway, marks PROCESSED.
    /// </summary>
    [HttpPost("{id:guid}/process")]
    [ProducesResponseType(typeof(ApiResponseDto<ProcessPayoutResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Process(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var staffId))
        {
            return Unauthorized(ApiResponseDto<ProcessPayoutResultDto>.Fail("Unauthorized"));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _payoutService.ProcessPayoutAsync(id, staffId, ipAddress, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
