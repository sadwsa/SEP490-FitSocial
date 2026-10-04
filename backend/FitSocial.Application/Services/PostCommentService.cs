using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class PostCommentService : IPostCommentService
{
    private readonly ICommentRepository _commentRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostCommentService>? _logger;

    public PostCommentService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<PostCommentService>? logger = null)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponseDto<List<CommentDto>>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        if (postId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required.");
        }

        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        var comments = await _commentRepository.GetCommentsByPostIdAsync(postId, cancellationToken);
        if (comments.Count == 0)
        {
            return ApiResponseDto<List<CommentDto>>.Ok(new List<CommentDto>(), "No comments found for this post.");
        }

        // Map all comments to DTOs
        var commentMap = comments.ToDictionary(
            c => c.Id,
            c => new CommentDto
            {
                Id = c.Id,
                PostId = c.PostId,
                AuthorId = c.AuthorId,
                AuthorName = !string.IsNullOrWhiteSpace(c.Author?.FullName)
                    ? c.Author.FullName
                    : (!string.IsNullOrWhiteSpace(c.Author?.Email) ? c.Author.Email : "FitSocial Member"),
                AuthorAvatarUrl = c.Author?.AvatarUrl,
                Content = c.Content ?? string.Empty,
                ParentCommentId = c.ParentCommentId,
                ReplyToAuthorName = c.ParentComment?.Author != null
                    ? (!string.IsNullOrWhiteSpace(c.ParentComment.Author.FullName)
                        ? c.ParentComment.Author.FullName
                        : c.ParentComment.Author.Email)
                    : null,
                CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = c.UpdatedAt,
                Replies = new List<CommentDto>()
            });

        var rootComments = new List<CommentDto>();

        // Build hierarchical tree:
        // Top-level comments (ParentCommentId == null)
        // Nested replies attached to their parent comment or root thread
        foreach (var c in comments)
        {
            var dto = commentMap[c.Id];
            if (!c.ParentCommentId.HasValue)
            {
                rootComments.Add(dto);
            }
            else if (commentMap.TryGetValue(c.ParentCommentId.Value, out var parentDto))
            {
                parentDto.Replies.Add(dto);
            }
            else
            {
                // If parent comment was not found in map, treat as root
                rootComments.Add(dto);
            }
        }

        return ApiResponseDto<List<CommentDto>>.Ok(rootComments, "Comments retrieved successfully.");
    }

    public async Task<ApiResponseDto<CommentDto>> CreateCommentAsync(
        Guid postId,
        Guid authorId,
        CreateCommentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (authorId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (postId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("Comment content cannot be empty.");
        }

        var trimmedContent = request.Content.Trim();
        if (trimmedContent.Length > 500)
        {
            throw new ValidationException("Comment content cannot exceed 500 characters.");
        }

        var user = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User account was not found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot comment on posts.");
        }

        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        Comment? parentComment = null;
        if (request.ParentCommentId.HasValue && request.ParentCommentId.Value != Guid.Empty)
        {
            parentComment = await _commentRepository.GetByIdWithAuthorAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment == null)
            {
                throw new NotFoundException("Parent comment was not found.");
            }

            if (parentComment.PostId != postId)
            {
                throw new ValidationException("Parent comment does not belong to this post.");
            }
        }

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = authorId,
            ParentCommentId = request.ParentCommentId.HasValue && request.ParentCommentId.Value != Guid.Empty
                ? request.ParentCommentId.Value
                : null,
            Content = trimmedContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _commentRepository.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("User {UserId} commented on Post {PostId}, CommentId {CommentId}", authorId, postId, comment.Id);

        var resultDto = new CommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            AuthorId = authorId,
            AuthorName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitSocial Member"),
            AuthorAvatarUrl = user.AvatarUrl,
            Content = comment.Content,
            ParentCommentId = comment.ParentCommentId,
            ReplyToAuthorName = parentComment?.Author != null
                ? (!string.IsNullOrWhiteSpace(parentComment.Author.FullName)
                    ? parentComment.Author.FullName
                    : parentComment.Author.Email)
                : null,
            CreatedAt = comment.CreatedAt.Value,
            UpdatedAt = comment.UpdatedAt,
            Replies = new List<CommentDto>()
        };

        return ApiResponseDto<CommentDto>.Ok(resultDto, "Comment posted successfully.");
    }

    public async Task<ApiResponseDto<CommentDto>> CreateReplyAsync(
        Guid commentId,
        Guid authorId,
        CreateReplyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (authorId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (commentId == Guid.Empty)
        {
            throw new ValidationException("Comment ID is required.");
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("Reply content cannot be empty.");
        }

        var trimmedContent = request.Content.Trim();
        if (trimmedContent.Length > 500)
        {
            throw new ValidationException("Reply content cannot exceed 500 characters.");
        }

        var user = await _userRepository.GetByIdAsync(authorId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User account was not found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot reply to comments.");
        }

        var parentComment = await _commentRepository.GetByIdWithAuthorAsync(commentId, cancellationToken);
        if (parentComment == null)
        {
            throw new NotFoundException($"Comment with ID '{commentId}' was not found.");
        }

        var post = await _postRepository.GetByIdAsync(parentComment.PostId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException("Post was not found or has been deleted.");
        }

        var reply = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = parentComment.PostId,
            AuthorId = authorId,
            ParentCommentId = commentId,
            Content = trimmedContent,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _commentRepository.AddAsync(reply, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation("User {UserId} replied to Comment {CommentId}, New ReplyId {ReplyId}", authorId, commentId, reply.Id);

        var resultDto = new CommentDto
        {
            Id = reply.Id,
            PostId = reply.PostId,
            AuthorId = authorId,
            AuthorName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitSocial Member"),
            AuthorAvatarUrl = user.AvatarUrl,
            Content = reply.Content,
            ParentCommentId = commentId,
            ReplyToAuthorName = parentComment.Author != null
                ? (!string.IsNullOrWhiteSpace(parentComment.Author.FullName)
                    ? parentComment.Author.FullName
                    : parentComment.Author.Email)
                : null,
            CreatedAt = reply.CreatedAt.Value,
            UpdatedAt = reply.UpdatedAt,
            Replies = new List<CommentDto>()
        };

        return ApiResponseDto<CommentDto>.Ok(resultDto, "Reply posted successfully.");
    }
}
