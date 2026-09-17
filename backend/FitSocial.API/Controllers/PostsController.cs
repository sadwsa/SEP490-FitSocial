using System.Security.Claims;
using FitSocial.Application.Commands.Posts;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IEditPostCommandHandler _editPostCommandHandler;

    public PostsController(
        IPostService postService,
        IEditPostCommandHandler editPostCommandHandler)
    {
        _postService = postService;
        _editPostCommandHandler = editPostCommandHandler;
    }

    /// <summary>
    /// Creates a new post.
    /// Requirements: posttype, sport, and location cannot be null, and media must not exceed 10 items (photos or videos).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            throw new FitSocial.Application.Exceptions.ValidationException(firstError ?? "Invalid post data.");
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new FitSocial.Application.Exceptions.ForbiddenException("Invalid session or user identifier not found.");
        }

        var currentUserRole = GetCurrentUserRole();
        var result = await _postService.CreatePostAsync(currentUserId.Value, currentUserRole, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Edits an existing post. Only the post author (owner) can edit.
    /// Supports keeping existing media, removing specific media IDs, and adding new media up to 10 total.
    /// </summary>
    [HttpPut("{postId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditPost(Guid postId, [FromBody] EditPostRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            throw new FitSocial.Application.Exceptions.ValidationException(firstError ?? "Invalid post data.");
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new FitSocial.Application.Exceptions.ForbiddenException("Invalid session or user identifier not found.");
        }

        var currentUserRole = GetCurrentUserRole();

        var command = new EditPostCommand
        {
            PostId = postId,
            CurrentUserId = currentUserId.Value,
            CurrentUserRole = currentUserRole,
            PostType = request.PostType,
            Content = request.Content,
            SportId = request.SportId,
            LocationId = request.LocationId,
            RemoveMediaIds = request.RemoveMediaIds ?? new(),
            NewMedia = request.NewMedia ?? new()
        };

        var result = await _editPostCommandHandler.HandleAsync(command, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Soft deletes an existing post.
    /// User can only delete their own post, unless the user has Admin or Staff privileges.
    /// </summary>
    [HttpDelete("{postId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid postId)
    {
        if (postId == Guid.Empty)
        {
            throw new FitSocial.Application.Exceptions.ValidationException("Post ID is required.");
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            throw new FitSocial.Application.Exceptions.ForbiddenException("Invalid session or user identifier not found.");
        }

        var currentUserRole = GetCurrentUserRole();
        var result = await _postService.DeletePostAsync(postId, currentUserId.Value, currentUserRole, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Search and filter posts feed with pagination.
    /// Supports keyword search by Content, filter by sportId, locationId, postType, and authorId.
    /// Accessible via:
    /// - GET /api/posts
    /// - GET /api/posts/search
    /// - GET /api/posts/filter
    /// </summary>
    [HttpGet]
    [HttpGet("search")]
    [HttpGet("filter")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<PostDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosts([FromQuery] GetPostsQueryDto query)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _postService.GetPostsAsync(query, currentUserId, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get post details by ID.
    /// If signed in, IsLikedByCurrentUser will reflect the user's like state.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostById(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _postService.GetPostByIdAsync(id, currentUserId, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Get posts authored by a specific user (e.g. for user profile view).
    /// </summary>
    [HttpGet("user/{authorId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<PostDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPostsByUser(
        Guid authorId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetPostsQueryDto
        {
            AuthorId = authorId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var currentUserId = GetCurrentUserId();
        var result = await _postService.GetPostsAsync(query, currentUserId, HttpContext.RequestAborted);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }
}
