using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class PostReactionService : IPostReactionService
{
    private readonly IPostReactionRepository _reactionRepository;
    private readonly IPostRepository _postRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PostReactionService>? _logger;

    public PostReactionService(
        IPostReactionRepository reactionRepository,
        IPostRepository postRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<PostReactionService>? logger = null)
    {
        _reactionRepository = reactionRepository;
        _postRepository = postRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ApiResponseDto<PostReactionResponseDto>> ToggleReactionAsync(
        Guid postId,
        Guid userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authenticated user validation
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (postId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required.");
        }

        // 2. User existence and account lock status
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User account was not found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("Your account is locked and cannot react to posts.");
        }

        // 3. Role validation: Only Trainees and Coaches can like/unlike posts
        var effectiveRole = !string.IsNullOrWhiteSpace(userRole) ? userRole : user.RoleCode;
        var normalizedRole = effectiveRole?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalizedRole != RoleConstants.Trainee && normalizedRole != RoleConstants.Coach)
        {
            throw new ForbiddenException("Only Trainees and Coaches can react to posts.");
        }

        // 4. Post existence
        var post = await _postRepository.GetByIdAsync(postId, cancellationToken);
        if (post == null)
        {
            throw new NotFoundException($"Post with ID '{postId}' was not found.");
        }

        // 5. Post deleted validation
        if (post.IsDeleted == true)
        {
            throw new BusinessException("Cannot react to a post that has been deleted.");
        }

        // 6. Toggle Like / Unlike
        var existingInteraction = await _reactionRepository.GetByUserAndPostAsync(postId, userId, cancellationToken);
        bool isLiked;
        string message;

        try
        {
            if (existingInteraction == null)
            {
                // Heart clicked: Like post
                var interaction = PostInteraction.Create(postId, userId);
                await _reactionRepository.AddAsync(interaction, cancellationToken);
                isLiked = true;
                message = "Post liked successfully.";
            }
            else
            {
                // Heart clicked again: Unlike post
                _reactionRepository.Remove(existingInteraction);
                isLiked = false;
                message = "Post unliked successfully.";
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger?.LogWarning(ex, "Concurrency / Duplicate key exception while toggling reaction for Post {PostId}, User {UserId}", postId, userId);
            var current = await _reactionRepository.GetByUserAndPostAsync(postId, userId, cancellationToken);
            isLiked = current != null;
            message = isLiked ? "Post liked successfully." : "Post unliked successfully.";
        }

        // 7. Get accurate like count
        var likeCount = await _reactionRepository.GetReactionCountAsync(postId, cancellationToken);

        _logger?.LogInformation("User {UserId} toggled reaction on Post {PostId}. IsLiked: {IsLiked}, LikeCount: {Count}",
            userId, postId, isLiked, likeCount);

        var resultDto = new PostReactionResponseDto
        {
            PostId = postId,
            IsLiked = isLiked,
            LikeCount = likeCount
        };

        return ApiResponseDto<PostReactionResponseDto>.Ok(resultDto, message);
    }
}
