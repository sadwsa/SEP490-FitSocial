using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Enums;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ReportPostServiceTests
{
    private const string StandardReason = "Spam or misleading content";

    private readonly Mock<IPostReportRepository> _reportRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostReportService _service;

    public ReportPostServiceTests()
    {
        _reportRepoMock = new Mock<IPostReportRepository>();
        _postRepoMock = new Mock<IPostRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();

        _service = new PostReportService(
            _reportRepoMock.Object,
            _postRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task ReportPostAsync_WhenValidInputWithReasonOnly_CreatesReportAndReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _reportRepoMock.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act
        var result = await _service.ReportPostAsync(postId, reporterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Post reported successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(reporterId, result.Data.ReporterId);
        Assert.Equal(StandardReason, result.Data.Reason);
        Assert.Equal(ReportStatus.Pending.ToString(), result.Data.Status);
        Assert.Empty(result.Data.MediaUrls);

        _reportRepoMock.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportedPostId == postId &&
            rep.ReporterId == reporterId &&
            rep.ReportedUserId == authorId &&
            rep.Reason == StandardReason &&
            rep.Status == ReportStatus.Pending.ToString()), It.IsAny<CancellationToken>()), Times.Once);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportPostAsync_WhenValidInputWithDescriptionAndMediaUrls_CreatesReportWithMediaAndReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _reportRepoMock.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreatePostReportRequestDto
        {
            Reason = "Inappropriate content",
            Description = "Contains prohibited advertisements",
            Type = "POST",
            MediaUrls = new List<string> { "https://fitsocial.com/evidence1.jpg" }
        };

        // Act
        var result = await _service.ReportPostAsync(postId, reporterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Inappropriate content", result.Data.Reason);
        Assert.Equal("Contains prohibited advertisements", result.Data.Description);
        Assert.Equal("POST", result.Data.Type);
        Assert.Single(result.Data.MediaUrls);
        Assert.Equal("https://fitsocial.com/evidence1.jpg", result.Data.MediaUrls[0]);

        _reportRepoMock.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportedPostId == postId &&
            rep.Description == "Contains prohibited advertisements" &&
            rep.ReportMedia.Count == 1), It.IsAny<CancellationToken>()), Times.Once);

        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task ReportPostAsync_WhenReporterNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Reporter account was not found.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenReporterIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var lockedReporter = new User
        {
            UserId = reporterId,
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedReporter);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Your account is locked and cannot report posts.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenPostNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal($"Post with ID '{postId}' was not found.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenPostIsDeleted_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        var deletedPost = new Post
        {
            Id = postId,
            AuthorId = Guid.NewGuid(),
            IsDeleted = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPost);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Cannot report a post that has been deleted.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenReportingOwnPost_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        var ownPost = new Post
        {
            Id = postId,
            AuthorId = reporterId,
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownPost);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("You cannot report your own post.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenActiveReportAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User
        {
            UserId = reporterId,
            IsLocked = false
        };

        var post = new Post
        {
            Id = postId,
            AuthorId = Guid.NewGuid(),
            IsDeleted = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("You have already submitted a pending report for this post.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.ReportPostAsync(postId, reporterId, request, cts.Token));
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task ReportPostAsync_WhenReporterIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.Empty;

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("User is not authenticated.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenPostIdIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.Empty;
        var reporterId = Guid.NewGuid();

        var request = new CreatePostReportRequestDto
        {
            Reason = StandardReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Post ID is required.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenReasonIsWhitespace_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var request = new CreatePostReportRequestDto
        {
            Reason = "   "
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Report reason is required and cannot be empty.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReportPostAsync_WhenReasonExceeds500Characters_ThrowsValidationException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var request = new CreatePostReportRequestDto
        {
            Reason = new string('A', 501)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));

        Assert.Equal("Report reason cannot exceed 500 characters.", ex.Message);
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

