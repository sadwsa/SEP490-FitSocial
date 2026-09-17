using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Commands.Posts;

public interface IDeletePostCommandHandler
{
    Task<ApiResponseDto<bool>> HandleAsync(DeletePostCommand command, CancellationToken cancellationToken = default);
}

public class DeletePostCommandHandler : IDeletePostCommandHandler
{
    private readonly IPostRepository _posts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePostCommandHandler(
        IPostRepository posts,
        IUserRepository users,
        IUnitOfWork unitOfWork)
    {
        _posts = posts;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<bool>> HandleAsync(DeletePostCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Validate Post ID
        if (command.PostId == Guid.Empty)
        {
            throw new ValidationException("Post ID is required and cannot be empty.");
        }

        if (command.CurrentUserId == Guid.Empty)
        {
            throw new ForbiddenException("Invalid session or user identifier not found.");
        }

        // 2. Retrieve the post
        var post = await _posts.GetByIdAsync(command.PostId, cancellationToken);
        if (post == null || post.IsDeleted == true)
        {
            throw new NotFoundException($"Post with ID '{command.PostId}' was not found.");
        }

        // 3. Authorization check: Author or Admin/Staff
        if (!post.CanBeDeletedBy(command.CurrentUserId, command.CurrentUserRole))
        {
            throw new ForbiddenException("You do not have permission to delete this post.");
        }

        // 4. Verify operating user lock status
        var user = await _users.GetByIdAsync(command.CurrentUserId, cancellationToken);
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
}
