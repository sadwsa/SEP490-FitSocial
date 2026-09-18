using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>
    /// Get notifications for the currently logged-in user (UC-15)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<NotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<List<NotificationDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<List<NotificationDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNotifications()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<NotificationDto>>.Fail("Invalid authentication session. Please log in again."));
        }

        var result = await _notificationService.GetUserNotificationsAsync(currentUserId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Mark a notification as read (UC-15)
    /// </summary>
    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<bool>.Fail("Invalid authentication session. Please log in again."));
        }

        var result = await _notificationService.MarkAsReadAsync(id, currentUserId);
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
    /// Mark all notifications as read for current user (UC-15)
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<bool>.Fail("Invalid authentication session. Please log in again."));
        }

        var result = await _notificationService.MarkAllAsReadAsync(currentUserId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
