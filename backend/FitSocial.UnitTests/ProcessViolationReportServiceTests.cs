using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ProcessViolationReportServiceTests
{
    private readonly Mock<IReportRepository> _reportRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<ICoachProfileRepository> _coachProfileRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<IPostRepository> _postRepoMock;
    private readonly ReportService _service;

    public ProcessViolationReportServiceTests()
    {
        _reportRepoMock = new Mock<IReportRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock = new Mock<IUnitOfWork>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _coachProfileRepoMock = new Mock<ICoachProfileRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _postRepoMock = new Mock<IPostRepository>();

        _service = new ReportService(
            _reportRepoMock.Object,
            _userRepoMock.Object,
            _uowMock.Object,
            _orderRepoMock.Object,
            _coachProfileRepoMock.Object,
            _notificationRepoMock.Object,
            _postRepoMock.Object);
    }

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - ProcessViolationReportAsync_WhenActionIsWarn_IncrementsWarningCountAndResolvesReport
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "USER"
    /// - Post: empty/null
    /// - RoleCode: "USER"
    /// - WarningCount: 0
    /// - Action: "WARN"
    /// - Reason: empty/null
    /// Expected: Return ReportListItemDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsWarn_IncrementsWarningCountAndResolvesReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var reportedUser = new User
        {
            UserId = reportedUserId,
            FullName = "John Doe",
            Email = "john@fitsocial.com",
            RoleCode = "USER",
            WarningCount = 0,
            IsLocked = false
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            ReportedUser = reportedUser,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _userRepoMock.Setup(u => u.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "WARN",
            Note = "First warning for inappropriate comment"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("RESOLVED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        Assert.Equal(1, reportedUser.WarningCount);
        Assert.False(reportedUser.IsLocked);

        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificationRepoMock.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == reportedUserId &&
            notif.Type == "ViolationWarning"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID02 - ProcessViolationReportAsync_WhenActionIsLockForRepeatedViolation_LocksAccountAndResolvesReport
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "USER"
    /// - Post: empty/null
    /// - RoleCode: "USER"
    /// - WarningCount: 2
    /// - Action: "LOCK"
    /// - Reason: empty/null
    /// Expected: Return ReportListItemDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsLockForRepeatedViolation_LocksAccountAndResolvesReport()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var reportedUser = new User
        {
            UserId = reportedUserId,
            FullName = "Repeated Offender",
            RoleCode = "USER",
            WarningCount = 2,
            IsLocked = false,
            TokenVersion = 1
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            ReportedUser = reportedUser,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _userRepoMock.Setup(u => u.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "LOCK",
            Note = "Third violation: locking account"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, reportedUser.WarningCount);
        Assert.True(reportedUser.IsLocked);
        Assert.Equal(staffId, reportedUser.LockedBy);
        Assert.Equal(2, reportedUser.TokenVersion);
        Assert.Equal("RESOLVED", report.Status);

        _notificationRepoMock.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == reportedUserId &&
            notif.Type == "ViolationLock"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID03 - ProcessViolationReportAsync_WhenActionIsBlockForCoachWithSoldPackages_BlocksProfileWithoutLockingAccount
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "USER"
    /// - Post: empty/null
    /// - RoleCode: "COACH (HasSoldPackages = true)"
    /// - WarningCount: 2
    /// - Action: "BLOCK"
    /// - Reason: empty/null
    /// Expected: Return ReportListItemDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsBlockForCoachWithSoldPackages_BlocksProfileWithoutLockingAccount()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Coach With Trainees",
            RoleCode = RoleConstants.Coach,
            WarningCount = 2,
            IsLocked = false
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Status = "ACTIVE"
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = coachId,
            ReportedUser = coachUser,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _userRepoMock.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _orderRepoMock.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _coachProfileRepoMock.Setup(c => c.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "BLOCK",
            Note = "Blocking coach profile due to repeated violations"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(3, coachUser.WarningCount);
        Assert.False(coachUser.IsLocked); // Unlocked so active packages can be fulfilled
        Assert.Equal("BLOCKED", coachProfile.Status);
        Assert.Equal("RESOLVED", report.Status);

        _notificationRepoMock.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == coachId &&
            notif.Type == "ViolationBlock"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID04 - ProcessViolationReportAsync_WhenActionIsDeletePost_SoftDeletesPostAndIncrementsWarningCount
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "POST"
    /// - Post: Found (IsDeleted = false)
    /// - RoleCode: "USER"
    /// - WarningCount: 1
    /// - Action: "DELETE_POST"
    /// - Reason: "Violates community guidelines"
    /// Expected: Return ReportListItemDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsDeletePost_SoftDeletesPostAndIncrementsWarningCount()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var author = new User
        {
            UserId = authorId,
            FullName = "Post Creator",
            RoleCode = "USER",
            WarningCount = 1
        };

        var post = new Post
        {
            Id = postId,
            AuthorId = authorId,
            Author = author,
            Content = "Prohibited post content",
            IsDeleted = false
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedPostId = postId,
            ReportedPost = post,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _postRepoMock.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _userRepoMock.Setup(u => u.GetByIdAsync(authorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(author);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Violates community guidelines"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(post.IsDeleted);
        Assert.Equal(2, author.WarningCount);
        Assert.Equal("RESOLVED", report.Status);

        _postRepoMock.Verify(p => p.Update(post), Times.Once);
        _notificationRepoMock.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == authorId &&
            notif.Type == "ViolationPostDeleted"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID05 - ProcessViolationReportAsync_WhenActionIsReject_MarksReportAsRejectedWithoutPenalizingUser
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: empty/null
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "REJECT"
    /// - Reason: empty/null
    /// Expected: Return ReportListItemDto with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsReject_MarksReportAsRejectedWithoutPenalizingUser()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "REJECT",
            Note = "Report is invalid"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("REJECTED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);

        _userRepoMock.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationRepoMock.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID06 - ProcessViolationReportAsync_WhenReportNotFound_ThrowsNotFoundException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: empty/null
    /// - TargetType: empty/null
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "WARN"
    /// - Reason: empty/null
    /// Expected: NotFoundException: Report not found.
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenReportNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Report?)null);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "WARN"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("Report not found.", ex.Message);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID07 - ProcessViolationReportAsync_WhenReportAlreadyProcessed_ThrowsBusinessException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "RESOLVED"
    /// - TargetType: empty/null
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "WARN"
    /// - Reason: empty/null
    /// Expected: BusinessException: Report has already been processed with status 'RESOLVED'.
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenReportAlreadyProcessed_ThrowsBusinessException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            Status = "RESOLVED"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "WARN"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("Report has already been processed with status 'RESOLVED'.", ex.Message);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID08 - ProcessViolationReportAsync_WhenLockRequestedForLowViolationCount_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "USER"
    /// - Post: empty/null
    /// - RoleCode: "USER"
    /// - WarningCount: 1
    /// - Action: "LOCK"
    /// - Reason: empty/null
    /// Expected: ValidationException: Action 'LOCK' is only allowed when user has more than 2 violations...
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenLockRequestedForLowViolationCount_ThrowsValidationException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var reportedUser = new User
        {
            UserId = reportedUserId,
            RoleCode = "USER",
            WarningCount = 1 // Next is 2, which is <= 2
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            ReportedUser = reportedUser,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _userRepoMock.Setup(u => u.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "LOCK"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Contains("Action 'LOCK' is only allowed when user has more than 2 violations", ex.Message);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID09 - ProcessViolationReportAsync_WhenLockCoachWithSoldPackages_ThrowsBusinessException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "USER"
    /// - Post: empty/null
    /// - RoleCode: "COACH (HasSoldPackages = true)"
    /// - WarningCount: 2
    /// - Action: "LOCK"
    /// - Reason: empty/null
    /// Expected: BusinessException: Cannot lock Coach account because this Coach has sold Training Packages...
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenLockCoachWithSoldPackages_ThrowsBusinessException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var coachUser = new User
        {
            UserId = coachId,
            RoleCode = RoleConstants.Coach,
            WarningCount = 2 // Next is 3
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = coachId,
            ReportedUser = coachUser,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _userRepoMock.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _orderRepoMock.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "LOCK"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Contains("Cannot lock Coach account because this Coach has sold Training Packages", ex.Message);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID10 - ProcessViolationReportAsync_WhenDeletePostOnAlreadyDeletedPost_ThrowsBusinessException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "POST"
    /// - Post: Deleted (IsDeleted = true)
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "DELETE_POST"
    /// - Reason: "Violates community guidelines"
    /// Expected: BusinessException: The reported post has already been deleted.
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenDeletePostOnAlreadyDeletedPost_ThrowsBusinessException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post
        {
            Id = postId,
            IsDeleted = true
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedPostId = postId,
            ReportedPost = post,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _postRepoMock.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Violates community guidelines"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("The reported post has already been deleted.", ex.Message);
        _uowMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID11 - ProcessViolationReportAsync_WhenActionIsEmpty_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: empty/null
    /// - TargetType: empty/null
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: " "
    /// - Reason: empty/null
    /// Expected: ValidationException: Action is required.
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsEmpty_ThrowsValidationException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var request = new ProcessViolationReportRequestDto
        {
            Action = "   "
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("Action is required.", ex.Message);
        _reportRepoMock.Verify(r => r.GetReportWithDetailsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// UTCID12 - ProcessViolationReportAsync_WhenActionIsInvalid_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: empty/null
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "BAN_USER"
    /// - Reason: empty/null
    /// Expected: ValidationException: Invalid action 'BAN_USER'. Allowed actions are WARN, BLOCK, LOCK, DELETE_POST, REJECT, DISMISS.
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenActionIsInvalid_ThrowsValidationException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "BAN_USER"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("Invalid action 'BAN_USER'. Allowed actions are WARN, BLOCK, LOCK, DELETE_POST, REJECT, DISMISS.", ex.Message);
    }

    /// <summary>
    /// UTCID13 - ProcessViolationReportAsync_WhenDeletePostReasonExceeds300Characters_ThrowsValidationException
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - TargetType: "POST"
    /// - Post: empty/null
    /// - RoleCode: empty/null
    /// - WarningCount: empty/null
    /// - Action: "DELETE_POST"
    /// - Reason: 301 characters
    /// Expected: ValidationException: Deletion reason cannot exceed 300 characters.
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task ProcessViolationReportAsync_WhenDeletePostReasonExceeds300Characters_ThrowsValidationException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            ReportedPostId = postId,
            Status = "PENDING"
        };

        _reportRepoMock.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = new string('A', 301)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessViolationReportAsync(reportId, staffId, request));

        Assert.Equal("Deletion reason cannot exceed 300 characters.", ex.Message);
    }

    #endregion
}
