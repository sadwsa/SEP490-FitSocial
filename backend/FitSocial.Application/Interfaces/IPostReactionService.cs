using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;

namespace FitSocial.Application.Interfaces;

public interface IPostReactionService
{
    /// <summary>
    /// Toggles Like/Unlike on a post for the authenticated user.
    /// </summary>
    Task<ApiResponseDto<PostReactionResponseDto>> ToggleReactionAsync(
        Guid postId,
        Guid userId,
        string userRole,
        CancellationToken cancellationToken = default);
}
