using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Validation;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;

namespace FitSocial.Application.Commands.Posts;

public interface IEditPostCommandHandler
{
    Task<ApiResponseDto<PostDto>> HandleAsync(EditPostCommand command, CancellationToken cancellationToken = default);
}

public class EditPostCommandHandler : IEditPostCommandHandler
{
    private readonly IPostRepository _posts;
    private readonly IUserRepository _users;
    private readonly ISportRepository _sports;
    private readonly ILocationRepository _locations;
    private readonly IUnitOfWork _unitOfWork;

    public EditPostCommandHandler(
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

    public async Task<ApiResponseDto<PostDto>> HandleAsync(EditPostCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate required fields (null, empty, whitespace checks)
        var (isValid, errorMessage) = EditPostCommandValidator.Validate(command);
        if (!isValid)
        {
            throw new ValidationException(errorMessage!);
        }

        // 2. Parse PostType & Validate Business Rule: Role vs PostType
        PostRolePolicy.TryParsePostType(command.PostType, out var parsedPostType);
        if (!PostRolePolicy.IsPostTypeAllowedForRole(command.CurrentUserRole, parsedPostType))
        {
            throw new ForbiddenException(
                $"Role '{command.CurrentUserRole}' is not permitted to select PostType '{parsedPostType}'. " +
                $"Trainee can only select Normal or FindCoach; Coach can select Normal, FindCoach, or FindTrainee.");
        }

        // 3. Check post existence
        var post = await _posts.GetByIdWithMediaAsync(command.PostId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{command.PostId}' was not found.");
        }

        // 4. Authorization: User must be the post author / owner
        if (!post.CanBeEditedBy(command.CurrentUserId))
        {
            throw new ForbiddenException("You do not have permission to edit this post. Only the author can edit their own post.");
        }

        // 5. Check user account lock status
        var user = await _users.GetByIdAsync(command.CurrentUserId, cancellationToken);
        if (user == null || user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot edit posts.");
        }

        // 6. Verify Sport and Location exist
        var sport = await _sports.GetByIdAsync(command.SportId, cancellationToken);
        if (sport == null)
        {
            throw new NotFoundException($"Selected sport with ID '{command.SportId}' does not exist.");
        }

        var location = await _locations.GetByIdAsync(command.LocationId, cancellationToken);
        if (location == null)
        {
            throw new NotFoundException($"Selected location with ID '{command.LocationId}' does not exist.");
        }

        // 7. Validate RemoveMediaIds: must all belong to this post
        var existingMediaList = post.PostMedia.ToList();
        var mediaToRemove = new List<PostMedium>();

        if (command.RemoveMediaIds != null && command.RemoveMediaIds.Count > 0)
        {
            var existingMediaIds = existingMediaList.Select(m => m.Id).ToHashSet();

            foreach (var removeId in command.RemoveMediaIds)
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
        var newMediaCount = command.NewMedia?.Count ?? 0;
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
            post.UpdateDetails(command.Content, parsedPostType, command.SportId, command.LocationId);

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
            if (command.NewMedia != null && command.NewMedia.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var item in command.NewMedia)
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
            SportId = sport.SportId,
            SportName = sport.SportName,
            LocationId = location.LocationId,
            LocationName = location.LocationName,
            LocationAddress = location.Address,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            LikeCount = post.PostInteractions.Count,
            CommentCount = post.Comments.Count,
            IsLikedByCurrentUser = false,
            Media = post.PostMedia
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
}
