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

    [Fact]
    public async Task CreateCommentAsync_WhenReplyingToValidParentComment_CreatesReplyWithParentInfo()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var parentCommentId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            IsLocked = false
        };
        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };
        var parentComment = new Comment
        {
            Id = parentCommentId,
            PostId = postId,
            AuthorId = parentAuthorId,
            Author = new User { UserId = parentAuthorId, FullName = "Jane Smith" },
            Content = "Initial post feedback."
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(parentCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto
        {
            ParentCommentId = parentCommentId,
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateCommentAsync(postId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(parentCommentId, result.Data.ParentCommentId);
        Assert.Equal("Jane Smith", result.Data.ReplyToAuthorName);
        Assert.Equal(StandardValidContent, result.Data.Content);

        _commentRepoMock.Verify(r => r.AddAsync(It.Is<Comment>(c => c.ParentCommentId == parentCommentId), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

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

    [Fact]
    public async Task CreateCommentAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var request = new CreateCommentRequestDto { Content = StandardValidContent };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal("User account was not found.", ex.Message);
        _postRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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

    [Fact]
    public async Task CreateCommentAsync_WhenParentCommentBelongsToDifferentPost_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var otherPostId = Guid.NewGuid();
        var parentCommentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var user = new User { UserId = authorId, FullName = StandardAuthorName, IsLocked = false };
        var post = new Post { Id = postId, IsDeleted = false };
        var parentCommentOfOtherPost = new Comment
        {
            Id = parentCommentId,
            PostId = otherPostId, // Mismatched post ID
            Content = "Comment on another post."
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(parentCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentCommentOfOtherPost);

        var request = new CreateCommentRequestDto
        {
            ParentCommentId = parentCommentId,
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateCommentAsync(postId, authorId, request));

        Assert.Equal("Parent comment does not belong to this post.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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

