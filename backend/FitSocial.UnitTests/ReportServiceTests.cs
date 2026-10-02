using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace FitSocial.UnitTests;

public class ReportServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachProfileRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IPostRepository> _mockPostRepo;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockCoachProfileRepo = new Mock<ICoachProfileRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockPostRepo = new Mock<IPostRepository>();

        _service = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object,
            _mockOrderRepo.Object,
            _mockCoachProfileRepo.Object,
            _mockNotificationRepo.Object,
            _mockPostRepo.Object);
    }


    [Fact]
    public async Task ResolveReportAsync_ShouldSetStatusAndResolverInfo()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var report = new Report
        {
            ReportId = reportId,
            Status = "PENDING",
            Reason = "Inappropriate content"
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ResolveReportRequestDto { Status = "RESOLVED", ResolutionNote = "Content removed" };

        // Act
        var result = await _service.ResolveReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("RESOLVED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        Assert.NotNull(report.ResolvedAt);
        Assert.NotNull(report.NotifiedReportedUserAt);
    }

    [Fact]
    public async Task SubmitAppealAsync_WhenReportedUserAppeals_ShouldSetPendingAppealAndMedia()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            Status = "RESOLVED"
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new SubmitAppealRequestDto
        {
            AppealContent = "I did not violate any terms. Please re-check the post context.",
            MediaUrls = new List<string> { "https://cdn.example.com/appeal-proof.jpg" }
        };

        // Act
        var result = await _service.SubmitAppealAsync(reportId, reportedUserId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("PENDING", report.AppealStatus);
        Assert.Equal(request.AppealContent, report.AppealContent);
        Assert.NotNull(report.AppealedAt);
        Assert.Single(report.ReportMedia);
        Assert.Equal("APPEAL", report.ReportMedia.First().MediaFor);
    }

    [Fact]
    public async Task ProcessViolationReport_UserViolation1_Warn_CountBecomes1_CreatesNotification()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 0, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "WARN" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, user.WarningCount);
        Assert.False(user.IsLocked);
        Assert.Equal("RESOLVED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == userId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.Type == "ViolationWarning" &&
            notif.Description!.Contains("Current violation count: 1")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_UserViolation2_Warn_CountBecomes2_CreatesNotification()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 1, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "WARN" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, user.WarningCount);
        Assert.False(user.IsLocked);
        Assert.Equal("RESOLVED", report.Status);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == userId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.Type == "ViolationWarning" &&
            notif.Description!.Contains("Current violation count: 2")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_UserViolation3_Lock_CountBecomes3_LocksAccount_CreatesNotification()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 2, RoleCode = RoleConstants.Trainee, IsLocked = false, TokenVersion = 1 };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "LOCK" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, user.WarningCount);
        Assert.True(user.IsLocked);
        Assert.Equal(staffId, user.LockedBy);
        Assert.Equal(2, user.TokenVersion);
        Assert.Equal("RESOLVED", report.Status);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == userId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.Type == "ViolationLock" &&
            notif.Description!.Contains("Your account has been locked due to exceeding the allowed number of violations")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_UserViolationCountLe2_TryingLock_ThrowsValidationException()
    {
        // Arrange: current count is 0, next count would be 1 (not > 2)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 0, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new ProcessViolationReportRequestDto { Action = "LOCK" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Equal(0, user.WarningCount);
        Assert.False(user.IsLocked);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_CoachNotSoldPackages_Violation3_CanLock()
    {
        // Arrange: Coach has NOT sold any packages
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = coachId, Status = "PENDING" };
        var coachUser = new User { UserId = coachId, WarningCount = 2, RoleCode = RoleConstants.Coach, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _mockOrderRepo.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "LOCK" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, coachUser.WarningCount);
        Assert.True(coachUser.IsLocked);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_CoachHasSoldPackages_TryingLock_ThrowsBusinessException_AndNoChanges()
    {
        // Arrange: Coach HAS sold packages
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = coachId, Status = "PENDING" };
        var coachUser = new User { UserId = coachId, WarningCount = 2, RoleCode = RoleConstants.Coach, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _mockOrderRepo.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new ProcessViolationReportRequestDto { Action = "LOCK" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("Cannot lock Coach account", ex.Message);
        Assert.Equal(2, coachUser.WarningCount);
        Assert.False(coachUser.IsLocked);
        Assert.Equal("PENDING", report.Status);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_CoachHasSoldPackages_Block_Succeeds_LeavesAccountUnlocked_BlocksProfile()
    {
        // Arrange: Coach HAS sold packages, Action is BLOCK
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = coachId, Status = "PENDING" };
        var coachUser = new User { UserId = coachId, WarningCount = 2, RoleCode = RoleConstants.Coach, IsLocked = false };
        var coachProfile = new CoachProfile { CoachId = coachId, Status = "ACTIVE" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _mockOrderRepo.Setup(o => o.HasSoldTrainingPackageAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockCoachProfileRepo.Setup(c => c.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "BLOCK" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, coachUser.WarningCount);
        Assert.False(coachUser.IsLocked); // Account is NOT locked so packages are not interrupted!
        Assert.Equal("BLOCKED", coachProfile.Status);
        Assert.Equal("RESOLVED", report.Status);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == coachId &&
            notif.ActorId == staffId &&
            notif.Type == "ViolationBlock" &&
            notif.Description!.Contains("active Training Packages, your account cannot be locked")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_RejectOrDismiss_DoesNotIncrementViolationCountOrNotify()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 1, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "REJECT" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("REJECTED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        Assert.Equal(1, user.WarningCount); // Unchanged!
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DatabaseFailure_ShouldRollbackTransaction()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 0, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection error"));

        var request = new ProcessViolationReportRequestDto { Action = "WARN" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_NotificationHasRequiredMetadata()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, Status = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 0, RoleCode = RoleConstants.Trainee, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto { Action = "WARN" };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == userId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.IsRead == false &&
            notif.CreatedAt != null &&
            !string.IsNullOrEmpty(notif.Description)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_Success_SoftDeletesPost_IncrementsViolationCount_ResolvesReport_CreatesNotification()
    {
        // Arrange (Test 1)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
        var author = new User { UserId = authorId, WarningCount = 1, RoleCode = RoleConstants.Trainee };
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
            Reason = "Violates community guidelines."
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.True(post.IsDeleted);
        Assert.NotNull(post.UpdatedAt);
        Assert.Equal(2, author.WarningCount);
        Assert.Equal("RESOLVED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        Assert.NotNull(report.ResolvedAt);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == authorId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.Type == "ViolationPostDeleted" &&
            notif.Description != null &&
            notif.Description.Contains("Violates community guidelines.")), It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessViolationReport_DeletePost_EmptyReason_ThrowsValidationException(string? emptyReason)
    {
        // Arrange (Test 2)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = emptyReason
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("Deletion reason is required", ex.Message);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_NonPostReport_ThrowsValidationException()
    {
        // Arrange (Test 3)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        // User report without ReportedPostId
        var report = new Report { ReportId = reportId, ReportedUserId = reportedUserId, ReportedPostId = null, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Some reason"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("only applicable for Post reports", ex.Message);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_PostNotFound_ThrowsNotFoundException()
    {
        // Arrange (Test 4)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Post violates rules"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_PostAlreadySoftDeleted_ThrowsBusinessException()
    {
        // Arrange (Test 5)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = true };
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Post violates rules"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("already been deleted", ex.Message);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_NotificationHasRequiredFieldsAndReason()
    {
        // Arrange (Test 6)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var specificReason = "Graphic violence content found in media.";

        var post = new Post { Id = postId, AuthorId = authorId, IsDeleted = false };
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
            Reason = specificReason
        };

        // Act
        await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == authorId &&
            notif.ActorId == staffId &&
            notif.ReferenceId == reportId &&
            notif.IsRead == false &&
            notif.CreatedAt != null &&
            notif.Description != null &&
            notif.Description.Contains(specificReason) &&
            notif.Description.Contains("Your post has been removed")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_NotificationFails_RollsBackTransaction()
    {
        // Arrange (Test 7)
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
            .ThrowsAsync(new InvalidOperationException("Failed to send notification."));

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Inappropriate post"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));

        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_AlreadyResolved_ThrowsBusinessException()
    {
        // Arrange (Duplicate Processing Audit)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        // Report was already resolved
        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "RESOLVED" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Post violates rules"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("already been processed", ex.Message);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_ReasonExceeds300Chars_ThrowsValidationException()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var postId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = new string('A', 301) // 301 chars > 300 limit
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.ProcessViolationReportAsync(reportId, staffId, request));
        Assert.Contains("cannot exceed 300 characters", ex.Message);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessViolationReport_DeletePost_PostReportMismatchedReportedUserId_TargetsActualPostAuthor()
    {
        // Arrange (Post Owner Audit)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var actualAuthorId = Guid.NewGuid();
        var staleReportedUserId = Guid.NewGuid(); // Mismatched ID in Report
        var postId = Guid.NewGuid();

        var post = new Post { Id = postId, AuthorId = actualAuthorId, IsDeleted = false };
        var actualAuthor = new User { UserId = actualAuthorId, WarningCount = 0, RoleCode = RoleConstants.Trainee };
        var report = new Report { ReportId = reportId, ReportedUserId = staleReportedUserId, ReportedPostId = postId, Status = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockPostRepo.Setup(p => p.GetByIdAsync(postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _mockUserRepo.Setup(u => u.GetByIdAsync(actualAuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actualAuthor);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ProcessViolationReportRequestDto
        {
            Action = "DELETE_POST",
            Reason = "Copyright violation"
        };

        // Act
        var result = await _service.ProcessViolationReportAsync(reportId, staffId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(actualAuthorId, report.ReportedUserId);
        Assert.Equal(1, actualAuthor.WarningCount);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif => notif.UserId == actualAuthorId), It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif => notif.UserId == staleReportedUserId), It.IsAny<CancellationToken>()), Times.Never);
    }
}

