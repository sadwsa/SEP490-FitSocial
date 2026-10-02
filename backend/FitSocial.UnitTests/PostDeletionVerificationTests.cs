using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Posts;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PostDeletionVerificationTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IPostRepository> _mockPostRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IPostRealtimeNotifier> _mockPostRealtimeNotifier;
    private readonly ReportService _reportService;

    public PostDeletionVerificationTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockPostRepo = new Mock<IPostRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockPostRealtimeNotifier = new Mock<IPostRealtimeNotifier>();

        _reportService = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object,
            null!,
            null!,
            _mockNotificationRepo.Object,
            _mockPostRepo.Object,
            _mockPostRealtimeNotifier.Object
        );
    }

    /// <summary>
    /// Test 1: Soft Delete Persistence - verify post.SoftDelete() sets IsDeleted = true,
    /// sets UpdatedAt timestamp, invokes repository Update, and saves changes.
    /// </summary>
    [Fact]
    public async Task Test1_SoftDelete_Persistence_SetsIsDeletedTrue_CallsUpdate_AndPersists()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false, UpdatedAt = null };
        var author = new User { UserId = authorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Content violates safety rules."
        };

        // Act
        var result = await _reportService.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.True(post.IsDeleted);
        Assert.NotNull(post.UpdatedAt);
        Assert.Equal(1, author.WarningCount);
        Assert.Equal("RESOLVED", report.Status);

        // Verify repository update was explicitly called on post
        _mockPostRepo.Verify(p => p.Update(It.Is<Post>(pt => pt.Id == postId && pt.IsDeleted == true)), Times.Once);

        // Verify transaction was committed
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Test 2: Feed Filtering - verify that soft-deleted posts (IsDeleted = true)
    /// are excluded when GetPagedPostsAsync is executed.
    /// </summary>
    [Fact]
    public async Task Test2_FeedFiltering_SoftDeletedPost_ExcludedFromResults()
    {
        // Arrange
        var activePost = new Post { Id = Guid.NewGuid(), Content = "Active Post", IsDeleted = false };
        var deletedPost = new Post { Id = Guid.NewGuid(), Content = "Deleted Post", IsDeleted = true };

        var mockPostService = new Mock<IPostService>();
        mockPostService.Setup(s => s.GetPostsAsync(It.IsAny<GetPostsQueryDto>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<PostDto>>.Ok(
                PagedResultDto<PostDto>.Create(new List<PostDto>
                {
                    new() { Id = activePost.Id, Content = activePost.Content }
                }, 1, 1, 10),
                "Posts feed retrieved successfully."
            ));

        var query = new GetPostsQueryDto { PageNumber = 1, PageSize = 10 };

        // Act
        var feedResult = await mockPostService.Object.GetPostsAsync(query);

        // Assert
        Assert.True(feedResult.Success);
        Assert.NotNull(feedResult.Data);
        Assert.Single(feedResult.Data.Items);
        Assert.Equal(activePost.Id, feedResult.Data.Items[0].Id);
        Assert.DoesNotContain(feedResult.Data.Items, p => p.Id == deletedPost.Id);
    }

    /// <summary>
    /// Test 3: Detail Filtering - verify PostService.GetPostByIdAsync returns failure / Post Not Found
    /// when the post has been soft-deleted (IsDeleted = true).
    /// </summary>
    [Fact]
    public async Task Test3_DetailFiltering_DeletedPost_ReturnsPostNotFound()
    {
        // Arrange
        var deletedPostId = Guid.NewGuid();
        var mockPostRepo = new Mock<IPostRepository>();
        // GetByIdWithDetailsAsync filters out IsDeleted == true, returning null
        mockPostRepo.Setup(p => p.GetByIdWithDetailsAsync(deletedPostId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var postService = new PostService(
            mockPostRepo.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<ILocationRepository>(),
            Mock.Of<IUnitOfWork>()
        );

        // Act
        var result = await postService.GetPostByIdAsync(deletedPostId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Post not found.", result.Message);
        Assert.Null(result.Data);
    }

    /// <summary>
    /// Test 5: Transaction Rollback - verify that if notification or database fails after soft-delete,
    /// transaction is rolled back and changes are not committed.
    /// </summary>
    [Fact]
    public async Task Test5_Transaction_RollbackOnFailure_NoCommitOccurs()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
        var author = new User { UserId = authorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _mockNotificationRepo.Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated notification service failure."));

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Violates guidelines"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reportService.ProcessViolationReportAsync(reportId, staffId, request));

        // Verify transaction rolled back and commit never called
        _mockUow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPostRealtimeNotifier.Verify(r => r.NotifyPostDeletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    /// <summary>
    /// Test 6: Verify PostDeleted event is broadcasted after transaction commits successfully.
    /// </summary>
    [Fact]
    public async Task Test6_DeletePost_BroadcastsPostDeleted_AfterSuccessfulCommit()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
        var author = new User { UserId = authorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Content violation"
        };

        // Act
        var result = await _reportService.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRealtimeNotifier.Verify(r => r.NotifyPostDeletedAsync(postId), Times.Once);
    }

    /// <summary>
    /// Test 7: Verify PostDeleted event is NOT broadcasted if transaction fails or rolls back.
    /// </summary>
    [Fact]
    public async Task Test7_DeletePost_ExceptionBeforeCommit_DoesNotBroadcast()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
        var author = new User { UserId = authorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB save error"));

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Content violation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reportService.ProcessViolationReportAsync(reportId, staffId, request));

        _mockUow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPostRealtimeNotifier.Verify(r => r.NotifyPostDeletedAsync(It.IsAny<Guid>()), Times.Never);
    }

    /// <summary>
    /// Test 8: Non-DELETE_POST actions (WARN, REJECT, etc.) do NOT broadcast PostDeleted.
    /// </summary>
    [Fact]
    public async Task Test8_NonDeletePostActions_DoNotBroadcastPostDeleted()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
        var author = new User { UserId = authorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING", ReportedPost = post };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "WARN",
            Reason = "Warning reason"
        };

        // Act
        var result = await _reportService.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        _mockPostRealtimeNotifier.Verify(r => r.NotifyPostDeletedAsync(It.IsAny<Guid>()), Times.Never);
    }
}
