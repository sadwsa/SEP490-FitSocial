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

    private User CreateStaffViewer(Guid? id = null, bool isLocked = false)
    {
        return new User
        {
            UserId = id ?? Guid.NewGuid(),
            FullName = "Staff Member",
            Email = "staff@fitsocial.com",
            RoleCode = RoleConstants.Staff,
            IsLocked = isLocked
        };
    }

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - GetReportsAsync_WhenDefaultQuery_ReturnsPagedReportListWithSuccess
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Found
    /// - CancellationToken: Active
    /// Expected: Return PagedResultDto<ReportListItemDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenDefaultQuery_ReturnsPagedReportListWithSuccess()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto();

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

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
        var result = await _service.GetReportsAsync(query, staff.UserId);

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

    /// <summary>
    /// UTCID02 - GetReportsAsync_WhenFilteredByPostTargetTypeAndStatus_ReturnsFilteredPostReports
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: "POST"
    /// - FilterParameters: Status="PENDING", Search="spam"
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Found
    /// - CancellationToken: Active
    /// Expected: Return PagedResultDto<ReportListItemDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenFilteredByPostTargetTypeAndStatus_ReturnsFilteredPostReports()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto
        {
            TargetType = "POST",
            Status = "PENDING",
            SearchTerm = "spam"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        var postAuthor = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Post Creator",
            RoleCode = "USER",
            WarningCount = 0
        };

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = postAuthor.UserId,
            Author = postAuthor,
            Content = "Spam advertisement post",
            IsDeleted = false
        };

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedPostId = post.Id,
            ReportedPost = post,
            Reason = "Spam post",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            "PENDING", "POST", null, null, null, "spam", It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        // Act
        var result = await _service.GetReportsAsync(query, staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalCount);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal("POST", item.TargetType);
        Assert.Equal(post.Id, item.TargetId);
        Assert.Equal("Spam advertisement post", item.ReportedPostContent);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            "PENDING", "POST", null, null, null, "spam", It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID03 - GetReportsAsync_WhenFilteredByUserTargetType_ReturnsFilteredUserReports
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: "USER"
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Found
    /// - CancellationToken: Active
    /// Expected: Return PagedResultDto<ReportListItemDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenFilteredByUserTargetType_ReturnsFilteredUserReports()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto
        {
            TargetType = "USER"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        var reportedUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Reported Regular User",
            Email = "reported@fitsocial.com",
            RoleCode = "USER",
            WarningCount = 2
        };

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedUserId = reportedUser.UserId,
            ReportedUser = reportedUser,
            Reason = "Abusive behavior",
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, "USER", null, null, null, null, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        // Act
        var result = await _service.GetReportsAsync(query, staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Items);
        Assert.Equal("USER", item.TargetType);
        Assert.Equal(reportedUser.UserId, item.TargetId);
        Assert.Equal("Reported Regular User", item.ReportedUserName);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            null, "USER", null, null, null, null, It.IsAny<int>(), It.IsAny<int>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID04 - GetReportsAsync_WhenReportedUserIsCoach_ChecksSoldTrainingPackages
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Found
    /// - CancellationToken: Active
    /// Expected: Return PagedResultDto<ReportListItemDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenReportedUserIsCoach_ChecksSoldTrainingPackages()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto();
        var coachId = Guid.NewGuid();

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

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
        var result = await _service.GetReportsAsync(query, staff.UserId);

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

    /// <summary>
    /// UTCID05 - GetReportsAsync_WhenStaffAccountIsLocked_ReturnsFailResult
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: true
    /// - DatabaseResults: Not found
    /// - CancellationToken: Active
    /// Expected: Return Fail: "Account is locked."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenStaffAccountIsLocked_ReturnsFailResult()
    {
        // Arrange
        var lockedStaff = CreateStaffViewer(isLocked: true);
        var query = new GetReportsQueryDto();

        _userRepoMock.Setup(u => u.GetByIdAsync(lockedStaff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedStaff);

        // Act
        var result = await _service.GetReportsAsync(query, lockedStaff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Account is locked.", result.Message);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID06 - GetReportsAsync_WhenViewerRoleIsUnauthorized_ReturnsFailResult
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: "USER"
    /// - IsLocked: false
    /// - DatabaseResults: Not found
    /// - CancellationToken: Active
    /// Expected: Return Fail: "Access denied. User is not authorized to view reports."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenViewerRoleIsUnauthorized_ReturnsFailResult()
    {
        // Arrange
        var unauthorizedUser = new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Regular Trainee",
            RoleCode = "USER",
            IsLocked = false
        };
        var query = new GetReportsQueryDto();

        _userRepoMock.Setup(u => u.GetByIdAsync(unauthorizedUser.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unauthorizedUser);

        // Act
        var result = await _service.GetReportsAsync(query, unauthorizedUser.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Access denied. User is not authorized to view reports.", result.Message);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID07 - GetReportsAsync_WhenInvalidTargetType_ReturnsFailResult
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: "COMMENT"
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Not found
    /// - CancellationToken: Active
    /// Expected: Return Fail: "Invalid target type. Allowed values are 'POST' or 'USER'."
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenInvalidTargetType_ReturnsFailResult()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto
        {
            TargetType = "COMMENT"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        // Act
        var result = await _service.GetReportsAsync(query, staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Invalid target type. Allowed values are 'POST' or 'USER'.", result.Message);

        _reportRepoMock.Verify(r => r.GetPagedReportsAsync(
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID08 - GetReportsAsync_WhenCancellationRequested_ReturnsFailResult
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: empty/null
    /// - IsLocked: false
    /// - DatabaseResults: Not found
    /// - CancellationToken: Cancelled
    /// Expected: Return Fail: "Error retrieving reports: Operation was canceled"
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID09 - GetReportsAsync_WhenNoReportsFound_ReturnsEmptyPagedListWithSuccess
    /// Condition:
    /// - Precondition: Database connection is active
    /// - TargetType: empty/null
    /// - FilterParameters: empty/null
    /// - RoleCode: "STAFF"
    /// - IsLocked: false
    /// - DatabaseResults: Not found
    /// - CancellationToken: Active
    /// Expected: Return PagedResultDto<ReportListItemDto> with Success=true
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetReportsAsync_WhenNoReportsFound_ReturnsEmptyPagedListWithSuccess()
    {
        // Arrange
        var staff = CreateStaffViewer();
        var query = new GetReportsQueryDto();

        _userRepoMock.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        _reportRepoMock.Setup(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        // Act
        var result = await _service.GetReportsAsync(query, staff.UserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Empty(result.Data.Items);
    }

    #endregion
}
