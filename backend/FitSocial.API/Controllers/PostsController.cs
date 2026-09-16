using System.Security.Claims;
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

    public PostsController(IPostService postService)
    {
        _postService = postService;
    }

    /// <summary>
    /// Creates a new post.
    /// Requirements: posttype, sport, and location cannot be null, and media must not exceed 10 items (photos or videos).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<PostDto>.Fail(firstError ?? "Invalid post data."));
        }

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized(ApiResponseDto<PostDto>.Fail("Invalid session. Please sign in again."));
        }

        var result = await _postService.CreatePostAsync(currentUserId.Value, request, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get paginated posts feed with optional filters (postType, sportId, locationId, authorId, searchTerm).
    /// If signed in, IsLikedByCurrentUser will reflect the user's like state.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<PostDto>>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(ApiResponseDto<PostDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostById(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _postService.GetPostByIdAsync(id, currentUserId, HttpContext.RequestAborted);
        if (!result.Success)
        {
            return NotFound(result);
        }

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
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
