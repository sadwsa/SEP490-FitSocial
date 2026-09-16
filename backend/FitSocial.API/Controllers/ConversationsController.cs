using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Conversations;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversationService;

    public ConversationsController(IConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    /// <summary>
    /// View Conversation List for the currently logged-in user (UC-16)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<ConversationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<List<ConversationDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<List<ConversationDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetConversations()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<ConversationDto>>.Fail("Invalid authentication session. Please log in again."));
        }

        var result = await _conversationService.GetUserConversationsAsync(currentUserId);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// View Conversation Detail and message history (UC-16.1)
    /// </summary>
    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<ConversationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<ConversationDetailDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<ConversationDetailDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<ConversationDetailDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<ConversationDetailDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetConversationDetail(Guid conversationId)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<ConversationDetailDto>.Fail("Invalid authentication session. Please log in again."));
        }

        var result = await _conversationService.GetConversationDetailAsync(conversationId, currentUserId);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }

            if (result.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("authorized", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("participant", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Send Message to a conversation (UC-16.1.1)
    /// </summary>
    [HttpPost("{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<MessageDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage(Guid conversationId, [FromBody] SendMessageRequestDto request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<MessageDto>.Fail("Invalid authentication session. Please log in again."));
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(ApiResponseDto<MessageDto>.Fail("Message content cannot be empty."));
        }

        var result = await _conversationService.SendMessageAsync(conversationId, currentUserId, request);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }

            if (result.Message.Contains("Forbidden", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("authorized", StringComparison.OrdinalIgnoreCase) ||
                result.Message.Contains("participant", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }
}
