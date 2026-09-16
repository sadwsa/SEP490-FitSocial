using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class PostService : IPostService
{
    private readonly IPostRepository _posts;
    private readonly IUserRepository _users;
    private readonly ISportRepository _sports;
    private readonly ILocationRepository _locations;
    private readonly IUnitOfWork _unitOfWork;

    public PostService(
        IPostRepository posts,
        IUserRepository users,
        ISportRepository sports,
        ILocationRepository locations,
        IUnitOfWork unitOfWork)
    {
        _posts = posts;
        _users = users;
        _sports = sports;
        _locations = locations;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<PostDto>> CreatePostAsync(
        Guid authorId,
        CreatePostRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // 1. Check author account
        var author = await _users.GetByIdAsync(authorId, cancellationToken);
        if (author == null)
        {
            return ApiResponseDto<PostDto>.Fail("Author account not found.");
        }

        if (author.IsLocked == true)
        {
            return ApiResponseDto<PostDto>.Fail("Your account is locked and cannot create posts.");
        }

        // 2. Validate PostType is not null or whitespace
        if (string.IsNullOrWhiteSpace(request.PostType))
        {
            return ApiResponseDto<PostDto>.Fail("PostType cannot be empty.");
        }

        // 3. Validate Sport is not null and exists
        if (request.SportId == Guid.Empty)
        {
            return ApiResponseDto<PostDto>.Fail("Sport cannot be empty.");
        }

        var sport = await _sports.GetByIdAsync(request.SportId, cancellationToken);
        if (sport == null)
        {
            return ApiResponseDto<PostDto>.Fail("Selected sport does not exist.");
        }

        // 4. Validate Location is not null and exists
        if (request.LocationId == Guid.Empty)
        {
            return ApiResponseDto<PostDto>.Fail("Location cannot be empty.");
        }

        var location = await _locations.GetByIdAsync(request.LocationId, cancellationToken);
        if (location == null)
        {
            return ApiResponseDto<PostDto>.Fail("Selected location does not exist.");
        }

        // 5. Validate media count (max 10, images or videos)
        if (request.Media != null && request.Media.Count > PostConstants.MaxMediaCount)
        {
            return ApiResponseDto<PostDto>.Fail($"A post can contain at most {PostConstants.MaxMediaCount} media items (photos or videos). Found {request.Media.Count}.");
        }

        // 6. Validate each media item
        if (request.Media != null && request.Media.Count > 0)
        {
            for (int i = 0; i < request.Media.Count; i++)
            {
                var item = request.Media[i];
                if (string.IsNullOrWhiteSpace(item.MediaUrl))
                {
                    return ApiResponseDto<PostDto>.Fail($"Media #{i + 1} is missing MediaUrl.");
                }

                var mediaType = item.MediaType?.Trim().ToUpperInvariant();
                if (mediaType != PostConstants.MediaTypes.Image && mediaType != PostConstants.MediaTypes.Video)
                {
                    return ApiResponseDto<PostDto>.Fail($"Media #{i + 1} has an invalid MediaType '{item.MediaType}'. Only IMAGE or VIDEO is allowed.");
                }
            }
        }

        // 7. Post must have text content or at least one media item
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasMedia = request.Media != null && request.Media.Count > 0;
        if (!hasContent && !hasMedia)
        {
            return ApiResponseDto<PostDto>.Fail("Post must contain text content or at least one image/video.");
        }

        // 8. Initialize Post entity
        var now = DateTime.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Content = request.Content?.Trim(),
            PostType = request.PostType.Trim(),
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

        // 10. Persist to database
        await _posts.AddAsync(post, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        var (items, totalCount) = await _posts.GetPagedPostsAsync(
            query.PostType,
            query.SportId,
            query.LocationId,
            query.AuthorId,
            query.SearchTerm,
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
