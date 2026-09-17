using FitSocial.Application.Commands.Posts;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FitSocial.Application.Services;

public class PostService : IPostService
{
    private readonly IPostRepository _posts;
    private readonly IUserRepository _users;
    private readonly ISportRepository _sports;
    private readonly ILocationRepository _locations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEditPostCommandHandler _editPostCommandHandler;
    private readonly IDeletePostCommandHandler _deletePostCommandHandler;
    private readonly ILogger<PostService>? _logger;

    public PostService(
        IPostRepository posts,
        IUserRepository users,
        ISportRepository sports,
        ILocationRepository locations,
        IUnitOfWork unitOfWork,
        IEditPostCommandHandler editPostCommandHandler,
        IDeletePostCommandHandler deletePostCommandHandler,
        ILogger<PostService>? logger = null)
    {
        _posts = posts;
        _users = users;
        _sports = sports;
        _locations = locations;
        _unitOfWork = unitOfWork;
        _editPostCommandHandler = editPostCommandHandler;
        _deletePostCommandHandler = deletePostCommandHandler;
        _logger = logger;
    }

    public Task<ApiResponseDto<PostDto>> EditPostAsync(EditPostCommand command, CancellationToken cancellationToken = default)
    {
        return _editPostCommandHandler.HandleAsync(command, cancellationToken);
    }

    public Task<ApiResponseDto<bool>> DeletePostAsync(Guid postId, Guid currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        var command = new DeletePostCommand
        {
            PostId = postId,
            CurrentUserId = currentUserId,
            CurrentUserRole = currentUserRole
        };
        return _deletePostCommandHandler.HandleAsync(command, cancellationToken);
    }

    public async Task<ApiResponseDto<PostDto>> CreatePostAsync(
        Guid authorId,
        string currentUserRole,
        CreatePostRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        _logger?.LogInformation("[Post] CreatePostAsync started for Author: {AuthorId}, MediaCount: {MediaCount}", authorId, request.Media?.Count ?? 0);

        // 1. Check author account and lock state
        var author = await _users.GetByIdAsync(authorId, cancellationToken);
        if (author == null)
        {
            throw new NotFoundException("Author account not found.");
        }

        if (author.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot create posts.");
        }

        // 2. Validate PostType and Role permission
        if (string.IsNullOrWhiteSpace(request.PostType))
        {
            throw new ValidationException("PostType is required and cannot be empty or whitespace.");
        }

        if (!PostRolePolicy.TryParsePostType(request.PostType, out var parsedPostType))
        {
            throw new ValidationException($"Invalid PostType '{request.PostType}'. Allowed types: Normal, FindCoach, FindTrainee.");
        }

        if (!PostRolePolicy.IsPostTypeAllowedForRole(currentUserRole, parsedPostType))
        {
            throw new ForbiddenException(
                $"Role '{currentUserRole}' is not permitted to select PostType '{parsedPostType}'. " +
                $"Trainee can only select Normal or FindCoach; Coach can select Normal, FindCoach, or FindTrainee.");
        }

        // 3. Validate Sport exists
        if (request.SportId == Guid.Empty)
        {
            throw new ValidationException("Sport is required and cannot be empty.");
        }

        var sport = await _sports.GetByIdAsync(request.SportId, cancellationToken);
        if (sport == null)
        {
            throw new NotFoundException($"Selected sport with ID '{request.SportId}' does not exist.");
        }

        // 4. Validate Location exists
        if (request.LocationId == Guid.Empty)
        {
            throw new ValidationException("Location is required and cannot be empty.");
        }

        var location = await _locations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            throw new NotFoundException($"Selected location with ID '{request.LocationId}' does not exist.");
        }

        // 5. Validate media count (max 10 total)
        if (request.Media != null && request.Media.Count > PostConstants.MaxMediaCount)
        {
            throw new BusinessException($"A post can contain at most {PostConstants.MaxMediaCount} media items in total (photos or videos combined). Found {request.Media.Count}.");
        }

        // 6. Validate each media item
        if (request.Media != null && request.Media.Count > 0)
        {
            for (int i = 0; i < request.Media.Count; i++)
            {
                var item = request.Media[i];
                if (item == null)
                {
                    throw new ValidationException($"Media item at index {i} cannot be null.");
                }

                if (string.IsNullOrWhiteSpace(item.MediaUrl))
                {
                    throw new ValidationException($"Media #{i + 1} is missing MediaUrl.");
                }

                if (string.IsNullOrWhiteSpace(item.MediaType))
                {
                    throw new ValidationException($"Media #{i + 1} is missing MediaType.");
                }

                var mediaType = item.MediaType.Trim().ToUpperInvariant();
                if (mediaType != PostConstants.MediaTypes.Image && mediaType != PostConstants.MediaTypes.Video)
                {
                    throw new ValidationException($"Media #{i + 1} has an invalid MediaType '{item.MediaType}'. Only IMAGE or VIDEO is allowed.");
                }
            }
        }

        // 7. Validate content requirement
        if (!string.IsNullOrWhiteSpace(request.Content) && request.Content.Length > PostConstants.MaxContentLength)
        {
            throw new ValidationException($"Content must not exceed {PostConstants.MaxContentLength} characters.");
        }

        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasMedia = request.Media != null && request.Media.Count > 0;
        if (!hasContent && !hasMedia)
        {
            throw new ValidationException("Post must contain text content or at least one image/video.");
        }

        // 8. Initialize Post entity
        var now = DateTime.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Content = request.Content?.Trim(),
            PostType = parsedPostType.ToString(),
            SportId = request.SportId,
            LocationId = request.LocationId,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        // 9. Add media items
        if (request.Media != null && request.Media.Count > 0)
        {
            foreach (var m in request.Media)
            {
                post.PostMedia.Add(new PostMedium
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    MediaUrl = m.MediaUrl.Trim(),
                    MediaType = m.MediaType.Trim().ToUpperInvariant(),
                    CreatedAt = now
                });
            }
        }

        // 10. Persist to database within transaction
        var dbSw = Stopwatch.StartNew();
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _posts.AddAsync(post, cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            dbSw.Stop();
            _logger?.LogInformation("[Post][DB] SaveChangesAsync/Transaction committed in {DbMs} ms", dbSw.ElapsedMilliseconds);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        // 11. Return response DTO
        var resultDto = new PostDto
        {
            Id = post.Id,
            AuthorId = author.UserId,
            AuthorName = author.FullName,
            AuthorAvatarUrl = author.AvatarUrl,
            Content = post.Content,
            PostType = post.PostType,
            SportId = sport.SportId,
            SportName = sport.SportName,
            LocationId = location.LocationId,
            LocationName = location.LocationName,
            LocationAddress = location.Address,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            Media = post.PostMedia.Select(pm => new PostMediaDto
            {
                Id = pm.Id,
                MediaUrl = pm.MediaUrl ?? string.Empty,
                MediaType = pm.MediaType ?? string.Empty,
                CreatedAt = pm.CreatedAt
            }).ToList()
        };

        totalSw.Stop();
        _logger?.LogInformation("[Post] CreatePostAsync completed in {TotalMs} ms", totalSw.ElapsedMilliseconds);

        return ApiResponseDto<PostDto>.Ok(resultDto, "Post created successfully.");
    }

    public async Task<ApiResponseDto<PostDto>> GetPostByIdAsync(
        Guid id,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetByIdWithDetailsAsync(id, cancellationToken);
        if (post == null)
        {
            return ApiResponseDto<PostDto>.Fail("Post not found.");
        }

        var resultDto = MapToPostDto(post, currentUserId);
        return ApiResponseDto<PostDto>.Ok(resultDto, "Post retrieved successfully.");
    }

    public async Task<ApiResponseDto<PagedResultDto<PostDto>>> GetPostsAsync(
        GetPostsQueryDto query,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate PageNumber & PageSize
        if (query.PageNumber < 1)
        {
            throw new ValidationException("PageNumber must be greater than or equal to 1.");
        }

        if (query.PageSize < 1)
        {
            throw new ValidationException("PageSize must be greater than or equal to 1.");
        }

        if (query.PageSize > 50)
        {
            throw new ValidationException("PageSize must not exceed 50 items per page.");
        }

        // 2. Validate PostType if specified
        string? normalizedPostType = null;
        if (!string.IsNullOrWhiteSpace(query.PostType))
        {
            if (!PostRolePolicy.TryParsePostType(query.PostType, out var parsedType))
            {
                throw new ValidationException($"Invalid PostType '{query.PostType}'. Allowed values: Normal, FindCoach, FindTrainee.");
            }
            normalizedPostType = parsedType.ToString();
        }

        // 3. Validate SportId if specified
        if (query.SportId.HasValue)
        {
            if (query.SportId.Value == Guid.Empty)
            {
                throw new ValidationException("SportId cannot be empty GUID.");
            }

            var sport = await _sports.GetByIdAsync(query.SportId.Value, cancellationToken);
            if (sport == null)
            {
                throw new NotFoundException($"Selected sport with ID '{query.SportId.Value}' does not exist.");
            }
        }

        // 4. Validate LocationId if specified
        if (query.LocationId.HasValue)
        {
            if (query.LocationId.Value == Guid.Empty)
            {
                throw new ValidationException("LocationId cannot be empty GUID.");
            }

            var location = await _locations.GetByIdAsync(query.LocationId.Value, cancellationToken);
            if (location == null)
            {
                throw new NotFoundException($"Selected location with ID '{query.LocationId.Value}' does not exist.");
            }
        }

        // 5. Query database with filtering and pagination
        var (items, totalCount) = await _posts.GetPagedPostsAsync(
            normalizedPostType,
            query.SportId,
            query.LocationId,
            query.AuthorId,
            query.EffectiveKeyword,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var dtos = items.Select(post => MapToPostDto(post, currentUserId)).ToList();

        var pagedResult = PagedResultDto<PostDto>.Create(dtos, totalCount, query.PageNumber, query.PageSize);
        return ApiResponseDto<PagedResultDto<PostDto>>.Ok(pagedResult, "Posts feed retrieved successfully.");
    }

    private static PostDto MapToPostDto(Post post, Guid? currentUserId)
    {
        return new PostDto
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            AuthorName = post.Author?.FullName,
            AuthorAvatarUrl = post.Author?.AvatarUrl,
            Content = post.Content,
            PostType = post.PostType,
            SportId = post.SportId ?? Guid.Empty,
            SportName = post.Sport?.SportName,
            LocationId = post.LocationId ?? Guid.Empty,
            LocationName = post.Location?.LocationName,
            LocationAddress = post.Location?.Address,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            LikeCount = post.PostInteractions.Count,
            CommentCount = post.Comments.Count,
            IsLikedByCurrentUser = currentUserId.HasValue && post.PostInteractions.Any(pi => pi.UserId == currentUserId.Value),
            Media = post.PostMedia.Select(pm => new PostMediaDto
            {
                Id = pm.Id,
                MediaUrl = pm.MediaUrl ?? string.Empty,
                MediaType = pm.MediaType ?? string.Empty,
                CreatedAt = pm.CreatedAt
            }).ToList()
        };
    }
}
