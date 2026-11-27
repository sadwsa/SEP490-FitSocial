using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostReactionServiceTests
{
    private readonly Mock<IPostReactionRepository> _reactionRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostReactionService _service;

    public PostReactionServiceTests()
    {
        _reactionRepoMock = new Mock<IPostReactionRepository>();
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();

        _service = new PostReactionService(
            _reactionRepoMock.Object,
            _postRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object);
    }

    #region Normal Cases (N) - UTCID01 to UTCID03

    /// <summary>
    /// UTCID01: Normal case - User has not liked post yet, heart clicked, post liked successfully and count incremented.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = false, Post = Found (IsDeleted = false), existingInteraction = null (Not liked), CancellationToken = Active.
    /// Confirm: Return PostReactionResponseDto with Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenPostNotLikedYet_LikesPostAndIncrementsCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var user = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            Content = "Fitness motivation post.",
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Trainee);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.True(result.Data.IsLiked);
        Assert.Equal(1, result.Data.LikeCount);
        Assert.Equal("Post liked successfully.", result.Message);

        _reactionRepoMock.Verify(r => r.AddAsync(It.Is<PostInteraction>(pi => pi.PostId == postId && pi.UserId == currentUserId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02: Normal case - User has already liked post, heart clicked again, post unliked successfully and count decremented.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "COACH", IsLocked = false, Post = Found (IsDeleted = false), existingInteraction = Found (Already liked), CancellationToken = Active.
    /// Confirm: Return PostReactionResponseDto with Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenAlreadyLiked_UnlikesPostAndDecrementsCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var existingInteraction = new PostInteraction { PostId = postId, UserId = currentUserId };

        var user = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Coach,
            FullName = "John Doe",
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            Content = "Workout tips.",
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInteraction);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Coach);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.False(result.Data.IsLiked);
        Assert.Equal(0, result.Data.LikeCount);
        Assert.Equal("Post unliked successfully.", result.Message);

        _reactionRepoMock.Verify(r => r.Remove(existingInteraction), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID03: Normal case - userRole argument is empty string, service gracefully falls back to user.RoleCode in database.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = empty/null, IsLocked = false, Post = Found (IsDeleted = false), existingInteraction = null (Not liked), CancellationToken = Active.
    /// Confirm: Return PostReactionResponseDto with Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenUserRoleArgumentIsEmpty_FallsBackToUserRoleCode()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var user = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            Content = "Post content.",
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act with empty string userRole
        var result = await _service.ToggleReactionAsync(postId, currentUserId, string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data!.IsLiked);
        Assert.Equal("Post liked successfully.", result.Message);
    }

    #endregion

    #region Abnormal Cases (A) - UTCID04 to UTCID08

    /// <summary>
    /// UTCID04: Abnormal case - User account is locked (IsLocked = true).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = true, Post = empty/null, existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: ForbiddenException
    /// Result: Type = A, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenUserAccountIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var lockedUser = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Trainee));

        Assert.Equal("Your account is locked and cannot react to posts.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID05: Abnormal case - User role is unauthorized to react (ADMIN).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "ADMIN", IsLocked = false, Post = empty/null, existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: ForbiddenException
    /// Result: Type = A, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenUserRoleIsUnauthorized_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var adminUser = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Admin,
            FullName = "Admin User",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Admin));

        Assert.Equal("Only Trainees and Coaches can react to posts.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID06: Abnormal case - Post does not exist in repository.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = false, Post = Not found, existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: NotFoundException
    /// Result: Type = A, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var user = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Trainee));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _reactionRepoMock.Verify(r => r.GetByUserAndPostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID07: Abnormal case - Post exists but is marked as soft-deleted (IsDeleted = true).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = false, Post = Deleted (IsDeleted = true), existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: BusinessException
    /// Result: Type = A, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenPostIsDeleted_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        var user = new User
        {
            UserId = currentUserId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        var deletedPost = new Post
        {
            Id = postId,
            Content = "Deleted post.",
            IsDeleted = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Trainee));

        Assert.Equal("Cannot react to a post that has been deleted.", ex.Message);
        _reactionRepoMock.Verify(r => r.GetByUserAndPostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID08: Abnormal case - Operation is cancelled via CancellationToken.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = false, Post = Found (IsDeleted = false), existingInteraction = empty/null, CancellationToken = Cancelled.
    /// Confirm: OperationCanceledException
    /// Result: Type = A, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.GetByIdAsync(currentUserId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.ToggleReactionAsync(postId, currentUserId, RoleConstants.Trainee, cts.Token));
    }

    #endregion

    #region Boundary Cases (B) - UTCID09 to UTCID10

    /// <summary>
    /// UTCID09: Boundary case - CurrentUserId is empty GUID (Guid.Empty).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, CurrentUserId = Guid.Empty, userRole = "TRAINEE", IsLocked = false, Post = empty/null, existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: ValidationException
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenCurrentUserIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var emptyUserId = Guid.Empty;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.ToggleReactionAsync(postId, emptyUserId, RoleConstants.Trainee));

        Assert.Equal("User is not authenticated.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID10: Boundary case - PostId is empty GUID (Guid.Empty).
    /// Conditions: Precondition = Database connection is active, postId = Guid.Empty, CurrentUserId = Valid GUID, userRole = "TRAINEE", IsLocked = false, Post = empty/null, existingInteraction = empty/null, CancellationToken = Active.
    /// Confirm: ValidationException
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task ToggleReactionAsync_WhenPostIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var emptyPostId = Guid.Empty;
        var currentUserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.ToggleReactionAsync(emptyPostId, currentUserId, RoleConstants.Trainee));

        Assert.Equal("Post ID is required.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
