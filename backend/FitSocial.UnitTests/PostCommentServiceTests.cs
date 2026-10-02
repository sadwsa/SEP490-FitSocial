using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostCommentServiceTests
{
    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostCommentService _service;

    public PostCommentServiceTests()
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

    [Fact]
    public async Task CreateComment_ValidRequest_CreatesAndReturnsComment()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User { UserId = authorId, FullName = "John Doe", IsLocked = false };
        var post = new Post { Id = postId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto { Content = "Great workout!" };

        // Act
        var result = await _service.CreateCommentAsync(postId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(authorId, result.Data.AuthorId);
        Assert.Equal("John Doe", result.Data.AuthorName);
        Assert.Equal("Great workout!", result.Data.Content);
        Assert.Null(result.Data.ParentCommentId);

        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateComment_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User { UserId = authorId, FullName = "Locked User", IsLocked = true };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new CreateCommentRequestDto { Content = "Hello" };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateCommentAsync(postId, authorId, request));
    }

    [Fact]
    public async Task CreateComment_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var user = new User { UserId = authorId, FullName = "John", IsLocked = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var request = new CreateCommentRequestDto { Content = "Hello" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateCommentAsync(postId, authorId, request));
    }

    [Fact]
    public async Task CreateReply_ValidRequest_CreatesReplyWithParentCommentId()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var parentCommentId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();

        var user = new User { UserId = authorId, FullName = "Jane Doe", IsLocked = false };
        var parentUser = new User { UserId = parentAuthorId, FullName = "John Doe" };
        var parentComment = new Comment
        {
            Id = parentCommentId,
            PostId = postId,
            AuthorId = parentAuthorId,
            Author = parentUser,
            Content = "Initial comment"
        };
        var post = new Post { Id = postId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(parentCommentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateReplyRequestDto { Content = "Thanks for your comment!" };

        // Act
        var result = await _service.CreateReplyAsync(parentCommentId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(authorId, result.Data.AuthorId);
        Assert.Equal(parentCommentId, result.Data.ParentCommentId);
        Assert.Equal("John Doe", result.Data.ReplyToAuthorName);
        Assert.Equal("Thanks for your comment!", result.Data.Content);

        _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCommentsByPostId_ReturnsHierarchicalComments()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var author1Id = Guid.NewGuid();
        var author2Id = Guid.NewGuid();

        var user1 = new User { UserId = author1Id, FullName = "User 1" };
        var user2 = new User { UserId = author2Id, FullName = "User 2" };

        var comment1Id = Guid.NewGuid();
        var reply1Id = Guid.NewGuid();

        var comment1 = new Comment
        {
            Id = comment1Id,
            PostId = postId,
            AuthorId = author1Id,
            Author = user1,
            Content = "First comment",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            ParentCommentId = null
        };

        var reply1 = new Comment
        {
            Id = reply1Id,
            PostId = postId,
            AuthorId = author2Id,
            Author = user2,
            Content = "Reply to first comment",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ParentCommentId = comment1Id,
            ParentComment = comment1
        };

        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = false });
        _commentRepoMock.Setup(r => r.GetCommentsByPostIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment> { comment1, reply1 });

        // Act
        var result = await _service.GetCommentsByPostIdAsync(postId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data); // 1 root comment
        Assert.Equal(comment1Id, result.Data[0].Id);
        Assert.Single(result.Data[0].Replies); // 1 reply nested under root
        Assert.Equal(reply1Id, result.Data[0].Replies[0].Id);
        Assert.Equal("User 1", result.Data[0].Replies[0].ReplyToAuthorName);
    }

    [Fact]
    public async Task GetCommentsByPostId_WhenNoComments_ReturnsEmptyList()
    {
        // Arrange
        var postId = Guid.NewGuid();
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, IsDeleted = false });
        _commentRepoMock.Setup(r => r.GetCommentsByPostIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Comment>());

        // Act
        var result = await _service.GetCommentsByPostIdAsync(postId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }
}
