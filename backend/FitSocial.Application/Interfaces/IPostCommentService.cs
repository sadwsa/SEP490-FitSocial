using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;

namespace FitSocial.Application.Interfaces;

public interface IPostCommentService
{
    Task<ApiResponseDto<List<CommentDto>>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CommentDto>> CreateCommentAsync(Guid postId, Guid authorId, CreateCommentRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CommentDto>> CreateReplyAsync(Guid commentId, Guid authorId, CreateReplyRequestDto request, CancellationToken cancellationToken = default);
}
