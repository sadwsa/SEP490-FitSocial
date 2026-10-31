using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class DeletePostServiceTests
{
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPostRealtimeNotifier> _realtimeNotifierMock;
    private readonly PostService _postService;

    public DeletePostServiceTests()
    {
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _locationRepoMock = new Mock<ILocationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _realtimeNotifierMock = new Mock<IPostRealtimeNotifier>();

        _postService = new PostService(
            _postRepoMock.Object,
            _userRepoMock.Object,
            _locationRepoMock.Object,
            _unitOfWorkMock.Object,
            logger: null,
            postRealtimeNotifier: _realtimeNotifierMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task DeletePostAsync_WhenAuthorDeletesOwnPost_ReturnsSuccessAndBroadcastsRealtime()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Post to be deleted by author.",
            IsDeleted = false
        };
        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        // Act
        var result = await _postService.DeletePostAsync(postId, authorId, "COACH");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Post deleted successfully.", result.Message);
        Assert.True(post.IsDeleted);
        Assert.NotNull(post.UpdatedAt);

        _postRepoMock.Verify(r => r.Update(post), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtimeNotifierMock.Verify(n => n.NotifyPostDeletedAsync(postId), Times.Once);
    }

    [Fact]
    public async Task DeletePostAsync_WhenAdminDeletesAnotherUsersPost_ReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Post authored by a user.",
            IsDeleted = false
        };
        var adminUser = new User
        {
            UserId = adminId,
            FullName = "Admin User",
            IsLocked = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Act
        var result = await _postService.DeletePostAsync(postId, adminId, "ADMIN");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.True(post.IsDeleted);

        _postRepoMock.Verify(r => r.Update(post), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtimeNotifierMock.Verify(n => n.NotifyPostDeletedAsync(postId), Times.Once);
    }

    [Fact]
    public async Task DeletePostAsync_WhenStaffDeletesAnotherUsersPost_ReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Violating post to be deleted by staff.",
            IsDeleted = false
        };
        var staffUser = new User
        {
            UserId = staffId,
            FullName = "Staff Member",
            IsLocked = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);

        // Act
        var result = await _postService.DeletePostAsync(postId, staffId, "STAFF");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.True(post.IsDeleted);

        _postRepoMock.Verify(r => r.Update(post), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtimeNotifierMock.Verify(n => n.NotifyPostDeletedAsync(postId), Times.Once);
    }

    [Fact]
    public async Task DeletePostAsync_WhenRealtimeNotifierFails_StillReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Content = "Post deleted with realtime failure.",
            IsDeleted = false
        };
        var author = new User
        {
            UserId = authorId,
            FullName = "John Doe",
            IsLocked = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _realtimeNotifierMock.Setup(n => n.NotifyPostDeletedAsync(postId))
            .ThrowsAsync(new Exception("SignalR connection dropped"));

        // Act
        var result = await _postService.DeletePostAsync(postId, authorId, "COACH");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Post deleted successfully.", result.Message);
        Assert.True(post.IsDeleted);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task DeletePostAsync_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _postService.DeletePostAsync(postId, currentUserId, "COACH"));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenPostIsAlreadyDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = true
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _postService.DeletePostAsync(postId, authorId, "COACH"));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenUserIsNotAuthorAndNotAdminOrStaff_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.DeletePostAsync(postId, otherUserId, "TRAINEE"));

        Assert.Equal("You do not have permission to delete this post.", ex.Message);
        _userRepoMock.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenUserDoesNotExistInDatabase_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = false
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.DeletePostAsync(postId, authorId, "COACH"));

        Assert.Equal("Your account is locked and cannot perform this action.", ex.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = false
        };
        var lockedAuthor = new User
        {
            UserId = authorId,
            FullName = "Locked User",
            IsLocked = true
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedAuthor);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.DeletePostAsync(postId, authorId, "COACH"));

        Assert.Equal("Your account is locked and cannot perform this action.", ex.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _postService.DeletePostAsync(postId, authorId, "COACH", cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task DeletePostAsync_WhenPostIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var emptyPostId = Guid.Empty;
        var currentUserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _postService.DeletePostAsync(emptyPostId, currentUserId, "COACH"));

        Assert.Equal("Post ID is required and cannot be empty.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePostAsync_WhenCurrentUserIdIsEmpty_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var emptyUserId = Guid.Empty;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _postService.DeletePostAsync(postId, emptyUserId, "COACH"));

        Assert.Equal("Invalid session or user identifier not found.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

