using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Creates a new order to purchase a training package (with snapshot details).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreatePackageOrderRequestDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<OrderDto>.Fail("Unauthorized"));
        }

        var result = await _orderService.CreatePackageOrderAsync(currentUserId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the current authenticated user's order history.
    /// </summary>
    [HttpGet("my-orders")]
    [ProducesResponseType(typeof(ApiResponseDto<List<OrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<OrderDto>>.Fail("Unauthorized"));
        }

        var result = await _orderService.GetMyOrdersAsync(currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a specific order.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<OrderDto>.Fail("Unauthorized"));
        }

        var result = await _orderService.GetOrderByIdAsync(id, currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets list of trainees and their purchased packages for the authenticated coach.
    /// </summary>
    [HttpGet("coach-trainees")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<List<CoachTraineeGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoachTraineeOrders(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var coachId))
        {
            return Unauthorized(ApiResponseDto<List<CoachTraineeGroupDto>>.Fail("Unauthorized: Invalid coach identity."));
        }

        var result = await _orderService.GetCoachTraineeOrdersAsync(coachId, cancellationToken);
        return Ok(result);
    }
}
