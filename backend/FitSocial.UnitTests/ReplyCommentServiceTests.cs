using System;
using System.Collections.Generic;
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

public class ReplyCommentServiceTests
{
    private const string StandardValidContent = "Thanks for the tip!";
    private const string StandardAuthorName = "John Doe";
    private const string StandardParentAuthorName = "Jane Smith";

    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostCommentService _service;

    public ReplyCommentServiceTests()
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
    /// UTCID01 - CreateReplyAsync_WhenValidInputWithAuthorFullName_CreatesAndReturnsReplyDto
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - parentComment: Found
    /// - parentAuthorFullName: "Jane Smith"
    /// - Post: Found (IsDeleted = false)
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Return CommentDto with Success=true, AuthorName and ReplyToAuthorName populated
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenValidInputWithAuthorFullName_CreatesAndReturnsReplyDto()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            Email = "user@fitsocial.com",
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };

        var parentAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = StandardParentAuthorName,
            Email = "parent@fitsocial.com"
        };

        var parentComment = new Comment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = parentAuthor.UserId,
            Author = parentAuthor,
            Content = "Initial comment"
        };

        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateReplyAsync(commentId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Reply posted successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(commentId, result.Data.ParentCommentId);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(authorId, result.Data.AuthorId);
        Assert.Equal(StandardAuthorName, result.Data.AuthorName);
        Assert.Equal(StandardParentAuthorName, result.Data.ReplyToAuthorName);
        Assert.Equal(StandardValidContent, result.Data.Content);

        _commentRepoMock.Verify(r => r.AddAsync(
            It.Is<Comment>(c => c.ParentCommentId == commentId && c.Content == StandardValidContent),
            It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02 - CreateReplyAsync_WhenAuthorFullNameIsEmpty_FallsBackToEmailAsAuthorName
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: Found
    /// - parentAuthorFullName: "Jane Smith"
    /// - Post: Found (IsDeleted = false)
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Return CommentDto with Success=true, AuthorName falls back to user email
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenAuthorFullNameIsEmpty_FallsBackToEmailAsAuthorName()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = null,
            Email = "user@fitsocial.com",
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };

        var parentAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = StandardParentAuthorName,
            Email = "parent@fitsocial.com"
        };

        var parentComment = new Comment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = parentAuthor.UserId,
            Author = parentAuthor,
            Content = "Initial comment"
        };

        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateReplyAsync(commentId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("user@fitsocial.com", result.Data.AuthorName);
        Assert.Equal(StandardParentAuthorName, result.Data.ReplyToAuthorName);
    }

    /// <summary>
    /// UTCID03 - CreateReplyAsync_WhenParentCommentAuthorFullNameIsEmpty_FallsBackToParentAuthorEmail
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - parentComment: Found
    /// - parentAuthorFullName: empty/null
    /// - Post: Found (IsDeleted = false)
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Return CommentDto with Success=true, ReplyToAuthorName falls back to parent author email
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenParentCommentAuthorFullNameIsEmpty_FallsBackToParentAuthorEmail()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            Email = "user@fitsocial.com",
            AvatarUrl = "https://fitsocial.com/avatar.jpg",
            IsLocked = false
        };

        var parentAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = null,
            Email = "parent@fitsocial.com"
        };

        var parentComment = new Comment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = parentAuthor.UserId,
            Author = parentAuthor,
            Content = "Initial comment"
        };

        var post = new Post
        {
            Id = postId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act
        var result = await _service.CreateReplyAsync(commentId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(StandardAuthorName, result.Data.AuthorName);
        Assert.Equal("parent@fitsocial.com", result.Data.ReplyToAuthorName);
    }

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID04 - CreateReplyAsync_WhenUserIsLocked_ThrowsForbiddenException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: true
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Throws ForbiddenException with message "Your account is locked and cannot reply to comments."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var lockedUser = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("Your account is locked and cannot reply to comments.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID05 - CreateReplyAsync_WhenParentCommentNotFound_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - parentComment: Not found
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Throws NotFoundException with message indicating comment was not found
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenParentCommentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Comment?)null);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal($"Comment with ID '{commentId}' was not found.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID06 - CreateReplyAsync_WhenTargetPostIsDeleted_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: "John Doe"
    /// - IsLocked: false
    /// - parentComment: Found
    /// - parentAuthorFullName: "Jane Smith"
    /// - Post: Deleted (IsDeleted = true)
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Throws NotFoundException with message "Post was not found or has been deleted."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenTargetPostIsDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var user = new User
        {
            UserId = authorId,
            FullName = StandardAuthorName,
            IsLocked = false
        };

        var parentAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = StandardParentAuthorName
        };

        var parentComment = new Comment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = parentAuthor.UserId,
            Author = parentAuthor
        };

        var deletedPost = new Post
        {
            Id = postId,
            IsDeleted = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("Post was not found or has been deleted.", ex.Message);
        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID07 - CreateReplyAsync_WhenCancellationRequested_ThrowsOperationCanceledException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Cancelled
    /// Expected: Throws OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID08 - CreateReplyAsync_WhenAuthorIdIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Guid.Empty
    /// - commentId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "User is not authenticated."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenAuthorIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.Empty;

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("User is not authenticated.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID09 - CreateReplyAsync_WhenCommentIdIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Guid.Empty
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: "Thanks for the tip!"
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Comment ID is required."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenCommentIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var commentId = Guid.Empty;
        var authorId = Guid.NewGuid();

        var request = new CreateReplyRequestDto
        {
            Content = StandardValidContent
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("Comment ID is required.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID10 - CreateReplyAsync_WhenContentIsWhitespace_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: " "
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Reply content cannot be empty."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenContentIsWhitespace_ThrowsValidationException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var request = new CreateReplyRequestDto
        {
            Content = "   "
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("Reply content cannot be empty.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID11 - CreateReplyAsync_WhenContentExceeds500Characters_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - authorId: Valid GUID
    /// - commentId: Valid GUID
    /// - FullName: empty/null
    /// - IsLocked: false
    /// - parentComment: empty/null
    /// - parentAuthorFullName: empty/null
    /// - Post: empty/null
    /// - Content: 501 characters
    /// - CancellationToken: Active
    /// Expected: Throws ValidationException with message "Reply content cannot exceed 500 characters."
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task CreateReplyAsync_WhenContentExceeds500Characters_ThrowsValidationException()
    {
        // Arrange
        var commentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var request = new CreateReplyRequestDto
        {
            Content = new string('A', 501)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateReplyAsync(commentId, authorId, request));

        Assert.Equal("Reply content cannot exceed 500 characters.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
