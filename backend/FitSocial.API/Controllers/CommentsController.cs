using System.Security.Claims;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommentsController : ControllerBase
{
    private readonly IPostCommentService _commentService;

    public CommentsController(IPostCommentService commentService)
    {
        _commentService = commentService;
    }

    
    // Replies to an existing comment.

    [HttpPost("{commentId:guid}/replies")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<CommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateReply(Guid commentId, [FromBody] CreateReplyRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            throw new FitSocial.Application.Exceptions.ValidationException(firstError ?? "Invalid reply data.");
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponseDto<object>.Fail("User is not authenticated."));
        }

        var result = await _commentService.CreateReplyAsync(commentId, currentUserId.Value, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
