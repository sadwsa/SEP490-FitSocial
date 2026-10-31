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
using Microsoft.EntityFrameworkCore;
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

    #region Normal Cases (N)

    [Fact]
    public async Task ToggleReactionAsync_WhenPostNotLikedYet_LikesPostAndIncrementsCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
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

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.True(result.Data.IsLiked);
        Assert.Equal(1, result.Data.LikeCount);
        Assert.Equal("Post liked successfully.", result.Message);

        _reactionRepoMock.Verify(r => r.AddAsync(It.Is<PostInteraction>(pi => pi.PostId == postId && pi.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenAlreadyLiked_UnlikesPostAndDecrementsCount()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingInteraction = new PostInteraction { PostId = postId, UserId = userId };

        var user = new User
        {
            UserId = userId,
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

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInteraction);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Coach);

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

    [Fact]
    public async Task ToggleReactionAsync_WhenDbUpdateExceptionOccurs_RecoversAndReturnsCurrentState()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var interaction = new PostInteraction { PostId = postId, UserId = userId };

        var user = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            Content = "High concurrency post.",
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // First call returns null, simulating heart click
        _reactionRepoMock.SetupSequence(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null)
            .ReturnsAsync(interaction); // In catch block, finds existing interaction from concurrent save

        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Duplicate key violation concurrent like"));

        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsLiked);
        Assert.Equal(5, result.Data.LikeCount);
        Assert.Equal("Post liked successfully.", result.Message);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenUserRoleArgumentIsEmpty_FallsBackToUserRoleCode()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
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

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reactionRepoMock.Setup(r => r.GetByUserAndPostAsync(postId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostInteraction?)null);
        _reactionRepoMock.Setup(r => r.GetReactionCountAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act with empty string userRole
        var result = await _service.ToggleReactionAsync(postId, userId, string.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data!.IsLiked);
        Assert.Equal("Post liked successfully.", result.Message);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task ToggleReactionAsync_WhenUserAccountNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));

        Assert.Equal("User account was not found.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenUserAccountIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var lockedUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));

        Assert.Equal("Your account is locked and cannot react to posts.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenUserRoleIsUnauthorized_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var adminUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Admin,
            FullName = "Admin User",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Admin));

        Assert.Equal("Only Trainees and Coaches can react to posts.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "John Doe",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _reactionRepoMock.Verify(r => r.GetByUserAndPostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenPostIsDeleted_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
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

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee));

        Assert.Equal("Cannot react to a post that has been deleted.", ex.Message);
        _reactionRepoMock.Verify(r => r.GetByUserAndPostAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleReactionAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.ToggleReactionAsync(postId, userId, RoleConstants.Trainee, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task ToggleReactionAsync_WhenUserIdIsEmpty_ThrowsValidationException()
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

    [Fact]
    public async Task ToggleReactionAsync_WhenPostIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var emptyPostId = Guid.Empty;
        var userId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.ToggleReactionAsync(emptyPostId, userId, RoleConstants.Trainee));

        Assert.Equal("Post ID is required.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
