using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CommentNotificationTests
{
    private readonly Mock<ICommentRepository> _commentRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<INotificationRealtimeNotifier> _realtimeNotifierMock;
    private readonly PostCommentService _service;

    public CommentNotificationTests()
    {
        _commentRepoMock = new Mock<ICommentRepository>();
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _realtimeNotifierMock = new Mock<INotificationRealtimeNotifier>();

        _service = new PostCommentService(
            _commentRepoMock.Object,
            _postRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object,
            _notificationRepoMock.Object,
            _realtimeNotifierMock.Object);
    }

    [Fact]
    public async Task CreateComment_WhenCommenterIsNotPostAuthor_SendsNotificationAndRealtimePushToPostAuthor()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var commenterId = Guid.NewGuid();

        var commenter = new User { UserId = commenterId, FullName = "Jane Doe", IsLocked = false };
        var post = new Post { Id = postId, AuthorId = postAuthorId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(commenterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(commenter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationRepoMock.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto { Content = "Awesome training tips!" };

        // Act
        var result = await _service.CreateCommentAsync(postId, commenterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        // Verify DB notification persisted for postAuthorId
        _notificationRepoMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == postAuthorId &&
            n.ActorId == commenterId &&
            n.Type == "POST_COMMENT" &&
            n.ReferenceId == postId &&
            n.Description.Contains("Jane Doe commented on your post")), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Realtime push notification sent to postAuthorId
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(
            postAuthorId,
            "POST_COMMENT",
            It.Is<string>(msg => msg.Contains("Jane Doe commented on your post"))), Times.Once);
    }

    [Fact]
    public async Task CreateComment_WhenCommenterIsPostAuthor_DoesNotSendNotification()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var author = new User { UserId = authorId, FullName = "Self Author", IsLocked = false };
        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateCommentRequestDto { Content = "Author commenting on their own post" };

        // Act
        var result = await _service.CreateCommentAsync(postId, authorId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        // Verify NO notification was sent
        _notificationRepoMock.Verify(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateReply_WhenReplierIsNotParentAuthor_SendsNotificationToParentAuthor()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var postAuthorId = Guid.NewGuid();
        var parentAuthorId = Guid.NewGuid();
        var replierId = Guid.NewGuid();
        var commentId = Guid.NewGuid();

        var replier = new User { UserId = replierId, FullName = "Bob Smith", IsLocked = false };
        var parentComment = new Comment
        {
            Id = commentId,
            PostId = postId,
            AuthorId = parentAuthorId,
            Author = new User { UserId = parentAuthorId, FullName = "Alice Coach" }
        };
        var post = new Post { Id = postId, AuthorId = postAuthorId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(replierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(replier);
        _commentRepoMock.Setup(r => r.GetByIdWithAuthorAsync(commentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentComment);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _commentRepoMock.Setup(r => r.AddAsync(It.IsAny<Comment>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notificationRepoMock.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateReplyRequestDto { Content = "I completely agree with this!" };

        // Act
        var result = await _service.CreateReplyAsync(commentId, replierId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        // 1. Verify notification sent to parentComment author
        _notificationRepoMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == parentAuthorId &&
            n.ActorId == replierId &&
            n.Type == "COMMENT_REPLY" &&
            n.Description.Contains("Bob Smith replied to your comment")), It.IsAny<CancellationToken>()), Times.Once);

        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(
            parentAuthorId,
            "COMMENT_REPLY",
            It.Is<string>(msg => msg.Contains("Bob Smith replied to your comment"))), Times.Once);

        // 2. Verify notification sent to postAuthorId (since postAuthor != replier && postAuthor != parentAuthor)
        _notificationRepoMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.UserId == postAuthorId &&
            n.ActorId == replierId &&
            n.Type == "POST_COMMENT" &&
            n.Description.Contains("Bob Smith commented on your post")), It.IsAny<CancellationToken>()), Times.Once);

        _realtimeNotifierMock.Verify(r => r.SendNotificationAsync(
            postAuthorId,
            "POST_COMMENT",
            It.Is<string>(msg => msg.Contains("Bob Smith commented on your post"))), Times.Once);
    }
}
