using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewReportListServiceTests
{
    private readonly Mock<IReportRepository> _reportRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<ICoachProfileRepository> _coachProfileRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly ReportService _service;

    public ViewReportListServiceTests()
    {
        _reportRepoMock = new Mock<IReportRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _coachProfileRepoMock = new Mock<ICoachProfileRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();

        _service = new ReportService(
            _reportRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object,
            _orderRepoMock.Object,
            _coachProfileRepoMock.Object,
            _notificationRepoMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task GetReportsAsync_WhenDefaultQuery_ReturnsPagedReportListWithSuccess()
    {
        // Arrange
        var query = new GetReportsQueryDto();

        var reporter = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "John Reporter",
            Email = "reporter@fitsocial.com"
        };

        var postAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Jane Author",
            Email = "author@fitsocial.com",
            RoleCode = "USER",
            WarningCount = 0
        };

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = postAuthor.UserId,
            Author = postAuthor,
            Content = "Sample reported post content",
            IsDeleted = false
        };

        var reportedUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Bob Violator",
            Email = "bob@fitsocial.com",
            RoleCode = "USER",
            WarningCount = 1
        };

        var report1 = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = reporter.UserId,
            Reporter = reporter,
            ReportedPostId = post.Id,
            ReportedPost = post,
            Reason = "Inappropriate post",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        var report2 = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = reporter.UserId,
            Reporter = reporter,
            ReportedUserId = reportedUser.UserId,
            ReportedUser = reportedUser,
            Reason = "Harassment user",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        var reports = new List<Report> { report1, report2 };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reports, 2));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Reports retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(10, result.Data.PageSize);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenFilteredByTargetTypeAndStatus_ReturnsFilteredResults()
    {
        // Arrange
        var query = new GetReportsQueryDto
        {
            TargetType = "post",
            Status = "PENDING",
            SearchTerm = "spam",
            Type = "SPAM",
            AppealStatus = "NONE",
            PageNumber = 2,
            PageSize = 20
        };

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedPostId = Guid.NewGuid(),
            Reason = "Spam post",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            "PENDING", "POST", null, null, null, "spam", 2, 20, "SPAM", "NONE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 21));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(21, result.Data.TotalCount);
        Assert.Equal(2, result.Data.PageNumber);
        Assert.Equal(20, result.Data.PageSize);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            "PENDING", "POST", null, null, null, "spam", 2, 20, "SPAM", "NONE", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenReportedUserIsCoach_ChecksSoldTrainingPackages()
    {
        // Arrange
        var query = new GetReportsQueryDto();
        var coachId = Guid.NewGuid();

        var coach = new User
        {
            UserId = coachId,
            FullName = "Coach Mike",
            RoleCode = RoleConstants.Coach,
            WarningCount = 3
        };

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedUserId = coachId,
            ReportedUser = coach,
            Reason = "Unprofessional conduct",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        _orderRepoMock.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.True(item.HasSoldTrainingPackage);
        Assert.Contains("BLOCK", item.AvailableActions);
        Assert.DoesNotContain("LOCK", item.AvailableActions);

        _orderRepoMock.Verify(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task GetReportsAsync_WhenInvalidTargetType_ReturnsFailResult()
    {
        // Arrange
        var query = new GetReportsQueryDto
        {
            TargetType = "COMMENT"
        };

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Invalid target type. Allowed values are 'POST' or 'USER'.", result.Message);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReportsAsync_WhenRepositoryThrowsException_ReturnsFailResult()
    {
        // Arrange
        var query = new GetReportsQueryDto();

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Error retrieving reports: Database connection failure", result.Message);
    }

    [Fact]
    public async Task GetReportsAsync_WhenCancellationRequested_ReturnsFailResult()
    {
        // Arrange
        var query = new GetReportsQueryDto();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.GetReportsAsync(query, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error retrieving reports", result.Message);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task GetReportsAsync_WhenQueryIsNull_AppliesDefaultsAndReturnsSuccess()
    {
        // Arrange
        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(null!);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(10, result.Data.PageSize);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenPageNumberLessThanOne_NormalizesToOne()
    {
        // Arrange
        var query = new GetReportsQueryDto
        {
            PageNumber = 0
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenPageSizeLessThanOne_NormalizesToTen()
    {
        // Arrange
        var query = new GetReportsQueryDto
        {
            PageSize = -5
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenPageSizeExceedsOneHundred_ClampsToOneHundred()
    {
        // Arrange
        var query = new GetReportsQueryDto
        {
            PageSize = 250
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 100, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 100, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportsAsync_WhenNoReportsFound_ReturnsEmptyPagedListWithSuccess()
    {
        // Arrange
        var query = new GetReportsQueryDto();

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Empty(result.Data.Items);
    }

    #endregion
}

