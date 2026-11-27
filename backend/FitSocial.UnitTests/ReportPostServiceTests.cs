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

    /// <summary>
    /// UTCID01 - ReportPostAsync_WhenValidInput_CreatesReportAndReturnsSuccess
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Found
    /// - IsLocked: false
    /// - Post: Found (IsDeleted = false)
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: Return PostReportResponseDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ReportPostAsync_WhenValidInput_CreatesReportAndReturnsSuccess()
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

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID02 - ReportPostAsync_WhenReporterNotFound_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Not found
    /// - IsLocked: false
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: NotFoundException ("Reporter account was not found.")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID03 - ReportPostAsync_WhenReporterIsLocked_ThrowsForbiddenException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Found
    /// - IsLocked: true
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: ForbiddenException ("Your account is locked and cannot report posts.")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID04 - ReportPostAsync_WhenPostNotFound_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Found
    /// - IsLocked: false
    /// - Post: Not found
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: NotFoundException ("Post with ID '{postId}' was not found.")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID05 - ReportPostAsync_WhenPostIsDeleted_ThrowsBusinessException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Found
    /// - IsLocked: false
    /// - Post: Deleted (IsDeleted = true)
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: BusinessException ("Cannot report a post that has been deleted.")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID06 - ReportPostAsync_WhenActiveReportAlreadyExists_ThrowsConflictException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: Found
    /// - IsLocked: false
    /// - Post: Found (IsDeleted = false)
    /// - HasActiveReport: true
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: ConflictException ("You have already submitted a pending report for this post.")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID07 - ReportPostAsync_WhenCancellationRequested_ThrowsOperationCanceledException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Cancelled
    /// Expected: OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID08 - ReportPostAsync_WhenPostIdIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Guid.Empty
    /// - Reporter: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: "Spam or misleading content"
    /// - CancellationToken: Active
    /// Expected: ValidationException ("Post ID is required.")
    /// Type: B
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID09 - ReportPostAsync_WhenReasonIsWhitespace_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: " "
    /// - CancellationToken: Active
    /// Expected: ValidationException ("Report reason is required and cannot be empty.")
    /// Type: B
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID10 - ReportPostAsync_WhenReasonExceeds500Characters_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - postId: Valid GUID
    /// - Reporter: empty/null
    /// - IsLocked: false
    /// - Post: empty/null
    /// - HasActiveReport: false
    /// - Reason: 501 characters
    /// - CancellationToken: Active
    /// Expected: ValidationException ("Report reason cannot exceed 500 characters.")
    /// Type: B
    /// Status: Passed
    /// </summary>
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
