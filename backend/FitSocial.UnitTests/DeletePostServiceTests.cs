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

    #region Normal Cases (N) - UTCID01 to UTCID03

    /// <summary>
    /// UTCID01: Normal case - Author deletes own post successfully.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = false, Post = Found (IsDeleted = false), PostOwnership = Current user is author, CancellationToken = Active.
    /// Confirm: Return Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
    [Fact]
    public async Task DeletePostAsync_WhenAuthorDeletesOwnPost_ReturnsSuccess()
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
    }

    /// <summary>
    /// UTCID02: Normal case - Admin deletes another user's post successfully.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "ADMIN", IsLocked = false, Post = Found (IsDeleted = false), PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: Return Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
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
    }

    /// <summary>
    /// UTCID03: Normal case - Staff deletes another user's post successfully.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "STAFF", IsLocked = false, Post = Found (IsDeleted = false), PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: Return Success=true
    /// Result: Type = N, Status = Passed
    /// </summary>
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
    }

    #endregion

    #region Abnormal Cases (A) - UTCID04 to UTCID08

    /// <summary>
    /// UTCID04: Abnormal case - Post does not exist in repository.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = false, Post = Not found, PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: NotFoundException
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID05: Abnormal case - Post is already soft-deleted (IsDeleted = true).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = false, Post = Deleted (IsDeleted = true), PostOwnership = Current user is author, CancellationToken = Active.
    /// Confirm: NotFoundException
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID06: Abnormal case - Operating user is not author and has non-administrative role (TRAINEE).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "TRAINEE", IsLocked = false, Post = Found (IsDeleted = false), PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: ForbiddenException
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID07: Abnormal case - User account is locked (IsLocked = true).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = true, Post = Found (IsDeleted = false), PostOwnership = Current user is author, CancellationToken = Active.
    /// Confirm: ForbiddenException
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID08: Abnormal case - Operation is cancelled via CancellationToken.
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = false, Post = Found (IsDeleted = false), PostOwnership = Current user is author, CancellationToken = Cancelled.
    /// Confirm: OperationCanceledException
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    #region Boundary Cases (B) - UTCID09 to UTCID10

    /// <summary>
    /// UTCID09: Boundary case - PostId is empty GUID (Guid.Empty).
    /// Conditions: Precondition = Database connection is active, postId = Guid.Empty, currentUserId = Valid GUID, currentUserRole = "COACH", IsLocked = false, Post = empty/null, PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: ValidationException
    /// Result: Type = B, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID10: Boundary case - CurrentUserId is empty GUID (Guid.Empty).
    /// Conditions: Precondition = Database connection is active, postId = Valid GUID, currentUserId = Guid.Empty, currentUserRole = "COACH", IsLocked = false, Post = empty/null, PostOwnership = Current user is not author, CancellationToken = Active.
    /// Confirm: ForbiddenException
    /// Result: Type = B, Status = Passed
    /// </summary>
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
