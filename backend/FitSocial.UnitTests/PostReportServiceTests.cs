using System;
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
    public async Task Scenario1_UserA_Reports_Post1_OwnedBy_UserB_Succeeds()
    {
        // Scenario 1: User A reports Post 1 owned by User B -> Should succeed and create report
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var post1 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userA, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post1, AuthorId = userB, IsDeleted = false });
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(post1, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreatePostReportRequestDto { Reason = "Spam content" };
        var result = await _service.ReportPostAsync(post1, userA, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(post1, result.Data.PostId);
        Assert.Equal(userA, result.Data.ReporterId);

        _reportRepoMock.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportedPostId == post1 &&
            rep.ReporterId == userA &&
            rep.ReportedUserId == userB &&
            rep.Reason == "Spam content"), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario2_UserA_Reports_Post1_OwnedBy_UserB_Again_PreventsDuplicate()
    {
        // Scenario 2: User A reports Post 1 owned by User B again -> Prevent duplicate report
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var post1 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userA, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post1, AuthorId = userB, IsDeleted = false });
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(post1, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already reported

        var request = new CreatePostReportRequestDto { Reason = "Spam content duplicate" };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ReportPostAsync(post1, userA, request));

        Assert.Equal("You have already submitted a pending report for this post.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Scenario3_UserA_Reports_Post2_OwnedBy_UserB_AllowedEvenIfPost1WasReported()
    {
        // Scenario 3: User A reports Post 2 owned by User B -> Allowed! Must NOT be blocked by Post 1 report.
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var post2 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userA, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post2, AuthorId = userB, IsDeleted = false });
        // Post 2 is NOT reported yet by User A
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(post2, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreatePostReportRequestDto { Reason = "Harassment or bullying" };
        var result = await _service.ReportPostAsync(post2, userA, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(post2, result.Data.PostId);
        Assert.Equal(userA, result.Data.ReporterId);

        _reportRepoMock.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportedPostId == post2 &&
            rep.ReporterId == userA &&
            rep.ReportedUserId == userB), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario4_UserA_Reports_Post3_OwnedBy_UserB_Allowed()
    {
        // Scenario 4: User A reports Post 3 owned by User B -> Allowed!
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var post3 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userA, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post3, AuthorId = userB, IsDeleted = false });
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(post3, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreatePostReportRequestDto { Reason = "Inappropriate content" };
        var result = await _service.ReportPostAsync(post3, userA, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(post3, result.Data.PostId);
    }

    [Fact]
    public async Task Scenario5_UserA_Reports_Post2_OwnedBy_UserC_Allowed()
    {
        // Scenario 5: User A reports Post 2 owned by User C -> Allowed!
        var userA = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var post2 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userA, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post2, AuthorId = userC, IsDeleted = false });
        _reportRepoMock.Setup(r => r.HasActiveReportAsync(post2, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new CreatePostReportRequestDto { Reason = "False information" };
        var result = await _service.ReportPostAsync(post2, userA, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(post2, result.Data.PostId);
    }

    [Fact]
    public async Task Scenario6_UserB_Reports_Post1_OwnedBy_UserB_FollowsSelfReportRule()
    {
        // Scenario 6: User B reports Post 1 owned by User B -> Prevented by self-report rule
        var userB = Guid.NewGuid();
        var post1 = Guid.NewGuid();

        _userRepoMock.Setup(r => r.GetByIdAsync(userB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userB, IsLocked = false });
        _postRepoMock.Setup(r => r.GetByIdAsync(post1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Post { Id = post1, AuthorId = userB, IsDeleted = false });

        var request = new CreatePostReportRequestDto { Reason = "Reporting myself" };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportPostAsync(post1, userB, request));

        Assert.Equal("You cannot report your own post.", ex.Message);
        _reportRepoMock.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasUserReportedPost_ReturnsCorrectStatus_ScopedToPost()
    {
        var reporterId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        _reportRepoMock.Setup(r => r.HasActiveReportAsync(postId, reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.HasUserReportedPostAsync(postId, reporterId);

        Assert.True(result.Success);
        Assert.True(result.Data);
    }
}
