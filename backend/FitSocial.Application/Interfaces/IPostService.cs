using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;

namespace FitSocial.Application.Interfaces;

public interface IPostService
{
    Task<ApiResponseDto<PostDto>> CreatePostAsync(Guid authorId, string currentUserRole, CreatePostRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PostDto>> EditPostAsync(Guid postId, Guid currentUserId, string currentUserRole, EditPostRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> DeletePostAsync(Guid postId, Guid currentUserId, string currentUserRole, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PostDto>> GetPostByIdAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PagedResultDto<PostDto>>> GetPostsAsync(GetPostsQueryDto query, Guid? currentUserId = null, CancellationToken cancellationToken = default);
}
