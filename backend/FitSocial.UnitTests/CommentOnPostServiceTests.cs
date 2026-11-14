using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CommentOnPostServiceTests
{
    private const string StandardValidContent = "Great workout routine!";
    private const string StandardAuthorName = "John Doe";

    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostCommentService _service;

    public CommentOnPostServiceTests()
    {
        _commentRepoMock = new Mock<ICommentRepository>();
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();

        _service = new PostCommentService(
            _commentRepoMock.Object,
            _postRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object);
    }

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - CreateCommentAsync_WhenValidRootComment_CreatesAndReturnsCommentDto
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - Post: Found (IsDeleted = false)
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Return CommentDto with Success=true, ParentCommentId is null
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenValidRootComment_CreatesAndReturnsCommentDto()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };
        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto
        {
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateCommentAsync(postId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(authorId, result.Data.AuthorId);
        Assert.Equal(StandardAuthorName, result.Data.AuthorName);
        Assert.Equal(StandardValidContent, result.Data.Content);
        Assert.Null(result.Data.ParentCommentId);
        Assert.Equal("Comment posted successfully.", result.Message);

        _commentRepoMock.Verify(r => r.AddAsync(It.Is<Comment>(c => c.PostId == postId && c.AuthorId == authorId && c.Content == StandardValidContent), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02 - CreateCommentAsync_WhenAuthorHasNoFullName_FallsBackToEmail
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: Found (IsDeleted = false)
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Return CommentDto with Success=true, AuthorName falls back to user email
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenAuthorHasNoFullName_FallsBackToEmail()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var userWithoutFullName = new User
        {
            UserId = authorId,
            FullName = null!,
            Email = "user@fitsocial.com",
            IsLocked = false
        };
        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWithoutFullName);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto
        {
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateCommentAsync(postId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("user@fitsocial.com", result.Data!.AuthorName);
    }

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID03 - CreateCommentAsync_WhenUserIsLocked_ThrowsForbiddenException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: true
    /// - Post: empty/null
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Throws ForbiddenException with message "Your account is locked and cannot comment on posts."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            IsLocked = true
        };
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal("Your account is locked and cannot comment on posts.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID04 - CreateCommentAsync_WhenPostNotFound_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - Post: Not found
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Throws NotFoundException with message indicating post was not found
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User { UserId = authorId, FullName = StandardAuthorName, IsLocked = false };
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID05 - CreateCommentAsync_WhenPostIsDeleted_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - Post: Deleted (IsDeleted = true)
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Throws NotFoundException with message indicating post was not found
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenPostIsDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User { UserId = authorId, FullName = StandardAuthorName, IsLocked = false };
        var deletedPost = new Post { Id = postId, IsDeleted = true };
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID06 - CreateCommentAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Cancelled
    /// Expected: Throws OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.CreateCommentAsync(postId, authorId, request, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID07 - CreateCommentAsync_WhenAuthorIdIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Guid.Empty
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "User is not authenticated."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenAuthorIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var emptyAuthorId = Guid.Empty;
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateCommentAsync(postId, emptyAuthorId, request));

        Assert.Equal("User is not authenticated.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID08 - CreateCommentAsync_WhenPostIdIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Guid.Empty
    /// - authorId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - Content: "Great workout routine!"
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Post ID is required."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenPostIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var emptyPostId = Guid.Empty;
        var authorId = Guid.NewGuid();
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateCommentAsync(emptyPostId, authorId, request));

        Assert.Equal("Post ID is required.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID09 - CreateCommentAsync_WhenContentIsWhitespace_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - Content: " "
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Comment content cannot be empty."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenContentIsWhitespace_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var request = new CreateCommentRequestDto { Content = "     " };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal("Comment content cannot be empty.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID10 - CreateCommentAsync_WhenContentExceeds500Characters_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - authorId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - Content: 501 characters
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Comment content cannot exceed 500 characters."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateCommentAsync_WhenContentExceeds500Characters_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var request = new CreateCommentRequestDto { Content = new string('A', 501) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal("Comment content cannot exceed 500 characters.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
