using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Enums;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostReportServiceTests
{
    private readonly Mock<IPostReportRepository> _reportRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly PostReportService _service;

    public PostReportServiceTests()
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

    [Fact]
    public async Task ReportPost_ValidReport_ReturnsSuccess()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        var reporter = new User { UserId = reporterId, IsLocked = false };
        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreatePostReportRequestDto { Reason = "Spam or misleading content" };

        // Act
        var result = await _service.ReportPostAsync(postId, reporterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(postId, result.Data.PostId);
        Assert.Equal(reporterId, result.Data.ReporterId);
        Assert.Equal("Spam or misleading content", result.Data.Reason);
        Assert.Equal(ReportStatus.Pending.ToString(), result.Data.Status);

        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportPost_NonExistingPost_ThrowsNotFoundException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = reporterId, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var request = new CreatePostReportRequestDto { Reason = "Inappropriate content" };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));
    }

    [Fact]
    public async Task ReportPost_DeletedPost_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = reporterId, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, AuthorId = Guid.NewGuid(), IsDeleted = true });

        var request = new CreatePostReportRequestDto { Reason = "Spam" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));
        Assert.Equal("Cannot report a post that has been deleted.", ex.Message);
    }

    [Fact]
    public async Task ReportPost_OwnPost_ThrowsBusinessException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, AuthorId = userId, IsDeleted = false });

        var request = new CreatePostReportRequestDto { Reason = "Reporting my own post" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportPostAsync(postId, userId, request));
        Assert.Equal("You cannot report your own post.", ex.Message);
    }

    [Fact]
    public async Task ReportPost_DuplicateReport_ThrowsConflictException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = reporterId, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = postId, AuthorId = Guid.NewGuid(), IsDeleted = false });
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreatePostReportRequestDto { Reason = "Spam post again" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));
        Assert.Equal("You have already submitted a pending report for this post.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ReportPost_EmptyReason_ThrowsValidationException(string? reason)
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var request = new CreatePostReportRequestDto { Reason = reason! };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));
    }

    [Fact]
    public async Task ReportPost_UnauthenticatedUser_ThrowsValidationException()
    {
        // Arrange
        var request = new CreatePostReportRequestDto { Reason = "Some reason" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportPostAsync(Guid.NewGuid(), Guid.Empty, request));
    }

    [Fact]
    public async Task ReportPost_LockedUser_ThrowsForbiddenException()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = reporterId, IsLocked = true });

        var request = new CreatePostReportRequestDto { Reason = "Spam" };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ReportPostAsync(postId, reporterId, request));
    }
}
