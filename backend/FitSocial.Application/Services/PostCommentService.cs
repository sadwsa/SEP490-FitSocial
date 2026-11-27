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
    private readonly INotificationRepository? _notificationRepository;
    private readonly INotificationRealtimeNotifier? _realtimeNotifier;
    private readonly ILogger<PostCommentService>? _logger;

    public PostCommentService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<PostCommentService>? logger = null)
        : this(commentRepository, postRepository, userRepository, unitOfWork, null, null, logger)
    {
    }

    public PostCommentService(
        ICommentRepository commentRepository,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        INotificationRepository? notificationRepository,
        INotificationRealtimeNotifier? realtimeNotifier,
        ILogger<PostCommentService>? logger = null)
    {
        _commentRepository = commentRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _notificationRepository = notificationRepository;
        _realtimeNotifier = realtimeNotifier;
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
                    : (!string.IsNullOrWhiteSpace(c.Author?.Email) ? c.Author.Email : "FitConnect Member"),
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
        var commentsById = comments.ToDictionary(c => c.Id);

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
                if (string.IsNullOrWhiteSpace(dto.ReplyToAuthorName))
                {
                    dto.ReplyToAuthorName = parentDto.AuthorName;
                }

                // Protect against circular references
                if (HasCycle(c.Id, c.ParentCommentId.Value, commentsById))
                {
                    _logger?.LogWarning("Cycle detected for comment {CommentId} referencing parent {ParentId}. Adding to root comments.", c.Id, c.ParentCommentId.Value);
                    rootComments.Add(dto);
                }
                else
                {
                    parentDto.Replies.Add(dto);
                }
            }
            else
            {
                // If parent comment was not found in map, treat as root
                rootComments.Add(dto);
            }
        }

        return ApiResponseDto<List<CommentDto>>.Ok(rootComments, "Comments retrieved successfully.");
    }

    private static bool HasCycle(Guid childId, Guid parentId, Dictionary<Guid, Comment> commentsById)
    {
        var visited = new HashSet<Guid> { childId };
        var current = parentId;
        while (visited.Add(current))
        {
            if (commentsById.TryGetValue(current, out var parent) && parent.ParentCommentId.HasValue)
            {
                current = parent.ParentCommentId.Value;
            }
            else
            {
                return false;
            }
        }
        return true;
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

        // Notify post author if commenter is not the post author
        if (post.AuthorId != authorId)
        {
            var commenterName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitConnect Member");

            var previewContent = trimmedContent.Length > 80
                ? trimmedContent.Substring(0, 77) + "..."
                : trimmedContent;

            var notificationMessage = $"{commenterName} commented on your post: \"{previewContent}\"";

            if (_notificationRepository != null)
            {
                try
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = post.AuthorId,
                        ActorId = authorId,
                        ReferenceId = postId,
                        Type = "POST_COMMENT",
                        Description = notificationMessage.Length > 500 ? notificationMessage.Substring(0, 497) + "..." : notificationMessage,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to persist comment notification for post owner {OwnerId}", post.AuthorId);
                }
            }

            if (_realtimeNotifier != null)
            {
                try
                {
                    await _realtimeNotifier.SendNotificationAsync(post.AuthorId, "POST_COMMENT", notificationMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send realtime comment notification to post owner {OwnerId}", post.AuthorId);
                }
            }
        }

        var resultDto = new CommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            AuthorId = authorId,
            AuthorName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitConnect Member"),
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

        var replierName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName
            : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitConnect Member");

        var replyPreview = trimmedContent.Length > 80
            ? trimmedContent.Substring(0, 77) + "..."
            : trimmedContent;

        // 1. Notify the author of the comment being replied to
        if (parentComment.AuthorId != authorId)
        {
            var replyMessage = $"{replierName} replied to your comment: \"{replyPreview}\"";

            if (_notificationRepository != null)
            {
                try
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = parentComment.AuthorId,
                        ActorId = authorId,
                        ReferenceId = parentComment.PostId,
                        Type = "COMMENT_REPLY",
                        Description = replyMessage.Length > 500 ? replyMessage.Substring(0, 497) + "..." : replyMessage,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to persist reply notification for comment author {CommentAuthorId}", parentComment.AuthorId);
                }
            }

            if (_realtimeNotifier != null)
            {
                try
                {
                    await _realtimeNotifier.SendNotificationAsync(parentComment.AuthorId, "COMMENT_REPLY", replyMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send realtime reply notification to comment author {CommentAuthorId}", parentComment.AuthorId);
                }
            }
        }

        // 2. Notify the post author if different from replier and parent comment author
        if (post.AuthorId != authorId && post.AuthorId != parentComment.AuthorId)
        {
            var postCommentMessage = $"{replierName} commented on your post: \"{replyPreview}\"";

            if (_notificationRepository != null)
            {
                try
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = post.AuthorId,
                        ActorId = authorId,
                        ReferenceId = parentComment.PostId,
                        Type = "POST_COMMENT",
                        Description = postCommentMessage.Length > 500 ? postCommentMessage.Substring(0, 497) + "..." : postCommentMessage,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationRepository.AddAsync(notification, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to persist reply notification for post owner {PostAuthorId}", post.AuthorId);
                }
            }

            if (_realtimeNotifier != null)
            {
                try
                {
                    await _realtimeNotifier.SendNotificationAsync(post.AuthorId, "POST_COMMENT", postCommentMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send realtime reply notification to post owner {PostAuthorId}", post.AuthorId);
                }
            }
        }

        var resultDto = new CommentDto
        {
            Id = reply.Id,
            PostId = reply.PostId,
            AuthorId = authorId,
            AuthorName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : "FitConnect Member"),
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
