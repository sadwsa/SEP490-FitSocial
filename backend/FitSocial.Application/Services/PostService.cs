using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Validation;
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
    private readonly ILocationRepository _locations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostService>? _logger;

    public PostService(
        IPostRepository posts,
        IUserRepository users,
        ILocationRepository locations,
        IUnitOfWork unitOfWork,
        ILogger<PostService>? logger = null)
    {
        _posts = posts;
        _users = users;
        _locations = locations;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponseDto<PostDto>> EditPostAsync(
        Guid postId,
        Guid currentUserId,
        string currentUserRole,
        EditPostRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate required fields (null, empty, whitespace checks)
        var (isValid, errorMessage) = EditPostRequestValidator.Validate(postId, currentUserId, request);
        if (!isValid)
        {
            throw new ValidationException(errorMessage!);
        }

        // 2. Parse PostType & Validate Business Rule: Role vs PostType
        PostRolePolicy.TryParsePostType(request.PostType, out var parsedPostType);
        if (!PostRolePolicy.IsPostTypeAllowedForRole(currentUserRole, parsedPostType))
        {
            throw new ForbiddenException(
                $"Role '{currentUserRole}' is not permitted to select PostType '{parsedPostType}'. " +
                $"Trainee can only select Normal or FindCoach; Coach can select Normal, FindCoach, or FindTrainee.");
        }

        // 3. Check post existence
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        // 4. Authorization: User must be the post author / owner
        if (!post.CanBeEditedBy(currentUserId))
        {
            throw new ForbiddenException("You do not have permission to edit this post. Only the author can edit their own post.");
        }

        // 5. Check user account lock status
        var user = await _users.GetByIdAsync(currentUserId, cancellationToken);
        if (user == null || user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot edit posts.");
        }

        // 6. Verify Location exists
        var location = await _locations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            throw new NotFoundException($"Selected location with ID '{request.LocationId}' does not exist.");
        }

        // 7. Validate RemoveMediaIds: must all belong to this post
        var existingMediaList = post.PostMedia.ToList();
        var mediaToRemove = new List<PostMedium>();

        if (request.RemoveMediaIds != null && request.RemoveMediaIds.Count > 0)
        {
            var existingMediaIds = existingMediaList.Select(m => m.Id).ToHashSet();

            foreach (var removeId in request.RemoveMediaIds)
            {
                if (!existingMediaIds.Contains(removeId))
                {
                    throw new BusinessException(
                        $"Media item with ID '{removeId}' does not belong to this post or does not exist.");
                }

                var mediaItem = existingMediaList.First(m => m.Id == removeId);
                mediaToRemove.Add(mediaItem);
            }
        }

        // 8. Validate media limit: at most 10 media items total (kept + new additions)
        var newMediaCount = request.NewMedia?.Count ?? 0;
        if (!Post.ValidateTotalMediaLimit(existingMediaList.Count, mediaToRemove.Count, newMediaCount, out var totalAfterEdit))
        {
            throw new BusinessException(
                $"A post cannot contain more than {PostConstants.MaxMediaCount} media items in total (photos or videos combined). " +
                $"Current kept: {existingMediaList.Count - mediaToRemove.Count}, New additions: {newMediaCount}. Total would be: {totalAfterEdit}.");
        }

        // 9. Execute update inside Database Transaction
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 9.1. Update post details
            post.UpdateDetails(request.Content, parsedPostType, request.LocationId);

            // 9.2. Remove requested media
            if (mediaToRemove.Count > 0)
            {
                _posts.RemoveMediaRange(mediaToRemove);
                foreach (var item in mediaToRemove)
                {
                    post.PostMedia.Remove(item);
                }
            }

            // 9.3. Add new media items
            if (request.NewMedia != null && request.NewMedia.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var item in request.NewMedia)
                {
                    var newMedium = new PostMedium
                    {
                        PostId = post.Id,
                        MediaUrl = item.MediaUrl.Trim(),
                        MediaType = item.MediaType.Trim().ToUpperInvariant(),
                        CreatedAt = now
                    };
                    post.PostMedia.Add(newMedium);
                }
            }

            // 9.4. Commit transaction
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        // 10. Return updated PostDto
        var responseDto = new PostDto
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            AuthorName = user.FullName,
            AuthorAvatarUrl = user.AvatarUrl,
            Content = post.Content,
            PostType = post.PostType,
            LocationId = location.LocationId,
            LocationName = location.LocationName,
            LocationAddress = location.Address,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            LikeCount = post.PostInteractions.Count,
            CommentCount = post.Comments.Count,
            IsLikedByCurrentUser = false,
            Media = post.PostMedia
                .OrderBy(pm => pm.CreatedAt)
                .Select(pm => new PostMediaDto
                {
                    Id = pm.Id,
                    MediaUrl = pm.MediaUrl ?? string.Empty,
                    MediaType = pm.MediaType ?? string.Empty,
                    CreatedAt = pm.CreatedAt
                }).ToList()
        };

        return ApiResponseDto<PostDto>.Ok(responseDto, "Post updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeletePostAsync(Guid postId, Guid currentUserId, string currentUserRole, CancellationToken cancellationToken = default)
    {
        // 1. Validate Post ID
        if (postId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required and cannot be empty.");
        }

        if (currentUserId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid session or user identifier not found.");
        }

        // 2. Retrieve the post
        var post = await _posts.GetByIdAsync(postId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        // 3. Authorization check: Author or Admin/Staff
        if (!post.CanBeDeletedBy(currentUserId, currentUserRole))
        {
            throw new ForbiddenException("You do not have permission to delete this post.");
        }

        // 4. Verify operating user lock status
        var user = await _users.GetByIdAsync(currentUserId, cancellationToken);
        if (user == null || user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot perform this action.");
        }

        // 5. Execute Soft Delete
        post.SoftDelete();

        // 6. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Post deleted successfully.");
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

        // 3. Validate Location exists
        if (request.LocationId == Guid.Empty)
        {
            throw new ValidationException("Location is required and cannot be empty.");
        }

        var location = await _locations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            throw new NotFoundException($"Selected location with ID '{request.LocationId}' does not exist.");
        }

        // 4. Validate media count (max 10 total)
        if (request.Media != null && request.Media.Count > PostConstants.MaxMediaCount)
        {
            throw new BusinessException($"A post can contain at most {PostConstants.MaxMediaCount} media items in total (photos or videos combined). Found {request.Media.Count}.");
        }

        // 5. Validate each media item
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

        // 6. Validate content requirement
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

        // 7. Initialize Post entity
        var now = DateTime.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Content = request.Content?.Trim(),
            PostType = parsedPostType.ToString(),
            LocationId = request.LocationId,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        // 8. Add media items
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

        // 9. Persist to database within transaction
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

        // 10. Return response DTO
        var resultDto = new PostDto
        {
            Id = post.Id,
            AuthorId = author.UserId,
            AuthorName = author.FullName,
            AuthorAvatarUrl = author.AvatarUrl,
            Content = post.Content,
            PostType = post.PostType,
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

        // 3. Validate LocationId if specified
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

        // 4. Query database with filtering and pagination
        var (items, totalCount) = await _posts.GetPagedPostsAsync(
            normalizedPostType,
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
