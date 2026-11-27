using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for Process Appeal feature:
/// 1. ReportsController.ReviewAppeal(Guid id, ReviewAppealRequestDto request, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID03): Staff approves 200 OK, Admin rejects + CancellationToken forwarding, [Authorize(Roles="ADMIN,STAFF")] check.
///    - Boundary (UTCID04 - UTCID05): Empty/whitespace NameIdentifier -> 401, Guid.Empty reportId forwarded -> NotFoundException.
///    - Abnormal (UTCID06 - UTCID09): Unauthenticated 401, malformed GUID claim 401, NotFoundException propagation, BusinessException/ValidationException propagation.
/// 2. ReportService.ReviewAppealAsync(Guid reportId, Guid staffId, ReviewAppealRequestDto request, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID07): Approve basic, Approve+RemoveViolation, Approve+UnblockAccount (User), Approve+UnblockAccount (CoachProfile), Reject ignores RemoveViolation/UnblockAccount, updates only PENDING Appeals, forwards CancellationToken.
///    - Boundary (UTCID08 - UTCID13): WarningCount=0 guard, WarningCount=1->0 boundary, UnblockAccount when already active/unlocked, ReportedUserId=null skip, null/whitespace note & empty Appeals, lowercase "approved" re-review guard.
///    - Abnormal (UTCID14 - UTCID20): Null request, empty AppealStatus, invalid AppealStatus, report not found, already reviewed (APPROVED/REJECTED), SaveChanges DB failure, OperationCanceledException.
/// 3. ReportRepository.GetReportWithDetailsByIdAsync(Guid reportId, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID02): Loads user report with Appeals/AppealMedia/Reporter/ReportedUser, loads post report with ReportedPost.Author.
///    - Boundary (UTCID03 - UTCID04): Missing/Guid.Empty returns null, empty Appeals & ReportMedia collections.
///    - Abnormal (UTCID05): Cancelled CancellationToken throws OperationCanceledException.
/// </summary>
public class ProcessAppealServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachProfileRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly ReportService _service;

    public ProcessAppealServiceTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockCoachProfileRepo = new Mock<ICoachProfileRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();

        _service = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object,
            _mockOrderRepo.Object,
            _mockCoachProfileRepo.Object,
            _mockNotificationRepo.Object);
    }

    #region ReportsController.ReviewAppeal (UTCID01 - UTCID09)

    private static ReportsController CreateControllerWithClaims(Mock<IReportService> mockReportService, params Claim[] claims)
    {
        var controller = new ReportsController(mockReportService.Object);
        var identity = claims.Length > 0
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenStaffApproves_ShouldReturn200Ok()
    {
        // Controller UTCID01 - Normal: Staff approves appeal -> 200 OK
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Staff));

        var request = new ReviewAppealRequestDto { AppealStatus = "APPROVED", AppealReviewNote = "Valid proof." };
        var expectedDto = new ReportListItemDto { ReportId = reportId, AppealStatus = "APPROVED", AppealReviewedBy = staffId };

        mockService.Setup(s => s.ReviewAppealAsync(reportId, staffId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<ReportListItemDto>.Ok(expectedDto, "Appeal reviewed successfully."));

        var actionResult = await controller.ReviewAppeal(reportId, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<ReportListItemDto>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Equal("Appeal reviewed successfully.", body.Message);
        Assert.Equal("APPROVED", body.Data!.AppealStatus);
        mockService.Verify(s => s.ReviewAppealAsync(reportId, staffId, request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenAdminRejectsWithCancellationToken_ShouldForwardTokenAndReturn200Ok()
    {
        // Controller UTCID02 - Normal: Admin rejects appeal and forwards explicit CancellationToken
        var mockService = new Mock<IReportService>();
        var adminId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Admin));

        var request = new ReviewAppealRequestDto { AppealStatus = "REJECTED", AppealReviewNote = "Insufficient evidence." };
        mockService.Setup(s => s.ReviewAppealAsync(reportId, adminId, request, token))
            .ReturnsAsync(ApiResponseDto<ReportListItemDto>.Ok(
                new ReportListItemDto { ReportId = reportId, AppealStatus = "REJECTED" },
                "Appeal reviewed successfully."));

        var actionResult = await controller.ReviewAppeal(reportId, request, token);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<ReportListItemDto>>(okResult.Value);
        Assert.Equal("REJECTED", body.Data!.AppealStatus);
        mockService.Verify(s => s.ReviewAppealAsync(reportId, adminId, request, token), Times.Once);
    }

    [Fact]
    public void ReportsController_ReviewAppeal_HasAuthorizeAdminOrStaffAttribute()
    {
        // Controller UTCID03 - Normal: Endpoint enforces ADMIN,STAFF authorization
        var method = typeof(ReportsController).GetMethod(nameof(ReportsController.ReviewAppeal));
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal($"{RoleConstants.Admin},{RoleConstants.Staff}", attr!.Roles);
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenNameIdentifierIsWhitespace_ShouldReturn401Unauthorized()
    {
        // Controller UTCID04 - Boundary: Empty/whitespace NameIdentifier -> 401 Unauthorized
        var mockService = new Mock<IReportService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "   "));

        var actionResult = await controller.ReviewAppeal(Guid.NewGuid(), new ReviewAppealRequestDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<ReportListItemDto>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Unauthorized", body.Message);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenReportIdIsEmptyGuid_ShouldDelegateToServiceAndPropagateNotFound()
    {
        // Controller UTCID05 - Boundary: Guid.Empty reportId passed to service -> NotFoundException propagates
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var request = new ReviewAppealRequestDto();
        mockService.Setup(s => s.ReviewAppealAsync(Guid.Empty, staffId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Report not found."));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.ReviewAppeal(Guid.Empty, request, CancellationToken.None));

        Assert.Equal("Report not found.", ex.Message);
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Controller UTCID06 - Abnormal: Missing user claims -> 401 Unauthorized
        var mockService = new Mock<IReportService>();
        var controller = CreateControllerWithClaims(mockService);

        var actionResult = await controller.ReviewAppeal(Guid.NewGuid(), new ReviewAppealRequestDto(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal("Unauthorized", Assert.IsType<ApiResponseDto<ReportListItemDto>>(unauthorized.Value).Message);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenNameIdentifierIsInvalidGuid_ShouldReturn401Unauthorized()
    {
        // Controller UTCID07 - Abnormal: Non-GUID NameIdentifier claim -> 401 Unauthorized
        var mockService = new Mock<IReportService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid"));

        var actionResult = await controller.ReviewAppeal(Guid.NewGuid(), new ReviewAppealRequestDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenServiceThrowsNotFoundException_ShouldPropagate()
    {
        // Controller UTCID08 - Abnormal: Report not found -> NotFoundException propagates
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var request = new ReviewAppealRequestDto();
        mockService.Setup(s => s.ReviewAppealAsync(reportId, staffId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Report not found."));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.ReviewAppeal(reportId, request, CancellationToken.None));
    }

    [Fact]
    public async Task ReportsController_ReviewAppeal_WhenServiceThrowsBusinessException_ShouldPropagate()
    {
        // Controller UTCID09 - Abnormal: Already reviewed appeal -> BusinessException propagates
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var request = new ReviewAppealRequestDto();
        mockService.Setup(s => s.ReviewAppealAsync(reportId, staffId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessException("This appeal has already been reviewed (APPROVED)."));

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            controller.ReviewAppeal(reportId, request, CancellationToken.None));

        Assert.Contains("already been reviewed", ex.Message);
    }

    #endregion

    #region ReportService.ReviewAppealAsync (UTCID01 - UTCID20)

    [Fact]
    public async Task ReviewAppealAsync_WhenApprovedBasic_ShouldUpdateReportAppealsNotifyAndSave()
    {
        // Service UTCID01 - Normal: Approving appeal updates Report, PENDING Appeal, sends notification, and saves
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var pendingAppeal = new Appeal { AppealId = Guid.NewGuid(), ReportId = reportId, Status = "PENDING" };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            AppealStatus = "PENDING",
            Appeals = new List<Appeal> { pendingAppeal }
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = reportedUserId, WarningCount = 1 });
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            AppealReviewNote = "  Clear misunderstanding.  ",
            RemoveViolation = false,
            UnblockAccount = false
        };

        var result = await _service.ReviewAppealAsync(reportId, staffId, request);

        Assert.True(result.Success);
        Assert.Equal("Appeal reviewed successfully.", result.Message);
        Assert.Equal("APPROVED", report.AppealStatus);
        Assert.Equal(staffId, report.AppealReviewedBy);
        Assert.NotNull(report.AppealReviewedAt);
        Assert.Equal("Clear misunderstanding.", report.AppealReviewNote);
        Assert.Equal("APPROVED", pendingAppeal.Status);
        Assert.Equal(staffId, pendingAppeal.ReviewedBy);
        Assert.Equal("Clear misunderstanding.", pendingAppeal.ReviewNote);
        Assert.NotNull(pendingAppeal.ReviewedAt);
        Assert.NotNull(pendingAppeal.UpdatedAt);

        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == reportedUserId &&
            notif.ActorId == staffId &&
            notif.Type == "ProcessAppeal" &&
            notif.ReferenceId == reportId &&
            notif.Description == "Your report appeal has been approved by staff."), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenApprovedWithRemoveViolation_ShouldDecrementWarningCount()
    {
        // Service UTCID02 - Normal: RemoveViolation=true decrements WarningCount (2 -> 1)
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedUserId = userId, AppealStatus = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 2, IsLocked = false };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            RemoveViolation = true
        });

        Assert.True(result.Success);
        Assert.Equal(1, user.WarningCount);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenApprovedWithUnblockAccountForLockedUser_ShouldUnlockUser()
    {
        // Service UTCID03 - Normal: UnblockAccount=true unlocks locked user and clears LockedBy
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedUserId = userId, AppealStatus = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 3, IsLocked = true, LockedBy = Guid.NewGuid() };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            RemoveViolation = true,
            UnblockAccount = true
        });

        Assert.True(result.Success);
        Assert.Equal(2, user.WarningCount);
        Assert.False(user.IsLocked);
        Assert.Null(user.LockedBy);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenApprovedWithUnblockAccountForBlockedCoach_ShouldActivateCoachProfile()
    {
        // Service UTCID04 - Normal: UnblockAccount=true reactivates BLOCKED CoachProfile to ACTIVE
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedUserId = coachId, AppealStatus = "PENDING" };
        var user = new User { UserId = coachId, RoleCode = RoleConstants.Coach, IsLocked = true, LockedBy = staffId };
        var coachProfile = new CoachProfile { CoachId = coachId, Status = "blocked" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockCoachProfileRepo.Setup(c => c.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync(coachProfile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            UnblockAccount = true
        });

        Assert.True(result.Success);
        Assert.False(user.IsLocked);
        Assert.Equal("ACTIVE", coachProfile.Status);
        Assert.NotNull(coachProfile.UpdatedAt);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenRejected_ShouldNotModifyUserOrCoachEvenIfFlagsTrue()
    {
        // Service UTCID05 - Normal: Trimmed/lowercase " rejected " sets REJECTED, sends reject notification, and ignores RemoveViolation/UnblockAccount
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedUserId = userId, AppealStatus = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "  rejected  ",
            AppealReviewNote = "Violation confirmed.",
            RemoveViolation = true,
            UnblockAccount = true
        });

        Assert.True(result.Success);
        Assert.Equal("REJECTED", report.AppealStatus);
        _mockUserRepo.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockCoachProfileRepo.Verify(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.Description == "Your report appeal has been reviewed and rejected."), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenReportHasPendingAndNonPendingAppeals_ShouldUpdateOnlyPendingAppeals()
    {
        // Service UTCID06 - Normal: Only Appeals with Status=="PENDING" are updated; others remain unchanged
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var pendingAppeal = new Appeal { AppealId = Guid.NewGuid(), Status = "PENDING" };
        var oldWithdrawnAppeal = new Appeal { AppealId = Guid.NewGuid(), Status = "WITHDRAWN", ReviewNote = "Original" };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = null,
            AppealStatus = "PENDING",
            Appeals = new List<Appeal> { pendingAppeal, oldWithdrawnAppeal }
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "REJECTED",
            AppealReviewNote = "Denied"
        });

        Assert.True(result.Success);
        Assert.Equal("REJECTED", pendingAppeal.Status);
        Assert.Equal("Denied", pendingAppeal.ReviewNote);
        Assert.Equal("WITHDRAWN", oldWithdrawnAppeal.Status);
        Assert.Equal("Original", oldWithdrawnAppeal.ReviewNote);
    }

    [Fact]
    public async Task ReviewAppealAsync_ShouldForwardCancellationTokenToAllAsyncDependencies()
    {
        // Service UTCID07 - Normal: Explicit CancellationToken is passed to repository, notification, user, coachProfile, and SaveChanges
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var report = new Report { ReportId = reportId, ReportedUserId = coachId, AppealStatus = "PENDING" };
        var user = new User { UserId = coachId, IsLocked = true };
        var profile = new CoachProfile { CoachId = coachId, Status = "BLOCKED" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, token)).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, token)).ReturnsAsync(user);
        _mockCoachProfileRepo.Setup(c => c.GetByIdAsync(coachId, token)).ReturnsAsync(profile);
        _mockUow.Setup(u => u.SaveChangesAsync(token)).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, staffId, new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            UnblockAccount = true
        }, token);

        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.GetReportWithDetailsByIdAsync(reportId, token), Times.Once);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), token), Times.Once);
        _mockUserRepo.Verify(u => u.GetByIdAsync(coachId, token), Times.Once);
        _mockCoachProfileRepo.Verify(c => c.GetByIdAsync(coachId, token), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenWarningCountIsZeroAndRemoveViolationTrue_ShouldNotBecomeNegative()
    {
        // Service UTCID08 - Boundary: WarningCount == 0 remains 0 when RemoveViolation=true
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, AppealStatus = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 0 };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            RemoveViolation = true
        });

        Assert.True(result.Success);
        Assert.Equal(0, user.WarningCount);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenWarningCountIsOneAndRemoveViolationTrue_ShouldDecrementToZero()
    {
        // Service UTCID09 - Boundary: WarningCount == 1 decrements cleanly to 0
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = userId, AppealStatus = "PENDING" };
        var user = new User { UserId = userId, WarningCount = 1 };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            RemoveViolation = true
        });

        Assert.True(result.Success);
        Assert.Equal(0, user.WarningCount);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenUserAlreadyUnlockedAndCoachActive_ShouldLeaveStateUnchanged()
    {
        // Service UTCID10 - Boundary: UnblockAccount=true when IsLocked=false and CoachProfile.Status="ACTIVE" performs no unintended mutation
        var reportId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = coachId, AppealStatus = "PENDING" };
        var user = new User { UserId = coachId, IsLocked = false, UpdatedAt = null };
        var profile = new CoachProfile { CoachId = coachId, Status = "ACTIVE", UpdatedAt = null };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockCoachProfileRepo.Setup(c => c.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            UnblockAccount = true
        });

        Assert.True(result.Success);
        Assert.False(user.IsLocked);
        Assert.Null(user.UpdatedAt);
        Assert.Equal("ACTIVE", profile.Status);
        Assert.Null(profile.UpdatedAt);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenReportedUserIdIsNull_ShouldSkipNotificationAndUserActions()
    {
        // Service UTCID11 - Boundary: ReportedUserId == null skips Notification and User/CoachProfile updates safely
        var reportId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, ReportedUserId = null, AppealStatus = "PENDING" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto
        {
            AppealStatus = "APPROVED",
            RemoveViolation = true,
            UnblockAccount = true
        });

        Assert.True(result.Success);
        _mockNotificationRepo.VerifyNoOtherCalls();
        _mockUserRepo.VerifyNoOtherCalls();
        _mockCoachProfileRepo.VerifyNoOtherCalls();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealReviewNoteIsNullAndAppealsEmpty_ShouldSucceed()
    {
        // Service UTCID12 - Boundary: Null AppealReviewNote and empty Appeals list handled safely when PENDING
        var reportId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, AppealStatus = "PENDING", Appeals = new List<Appeal>() };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto
        {
            AppealStatus = "REJECTED",
            AppealReviewNote = null
        });

        Assert.True(result.Success);
        Assert.Null(report.AppealReviewNote);
        Assert.Equal("REJECTED", report.AppealStatus);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenExistingAppealStatusIsNotPending_ShouldThrowBusinessException()
    {
        // Abnormal: Non-pending (null) appeal cannot be reviewed
        var reportId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, AppealStatus = null, Appeals = new List<Appeal>() };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "REJECTED" }));

        Assert.Contains("Only pending appeals", ex.Message);
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenExistingAppealStatusIsLowercaseApproved_ShouldBlockReReview()
    {
        // Service UTCID13 - Boundary: Case-insensitive re-review guard blocks lowercase "approved"
        var reportId = Guid.NewGuid();
        var report = new Report { ReportId = reportId, AppealStatus = "approved" };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "REJECTED" }));

        Assert.Contains("already been reviewed", ex.Message);
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenRequestIsNull_ShouldThrowValidationException()
    {
        // Service UTCID14 - Abnormal: Null request throws ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewAppealAsync(Guid.NewGuid(), Guid.NewGuid(), null!));

        Assert.Equal("AppealStatus is required.", ex.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealStatusIsEmptyOrWhitespace_ShouldThrowValidationException()
    {
        // Service UTCID15 - Abnormal: Empty/whitespace AppealStatus throws ValidationException
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewAppealAsync(Guid.NewGuid(), Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "   " }));

        Assert.Equal("AppealStatus is required.", ex.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealStatusIsInvalid_ShouldThrowValidationException()
    {
        // Service UTCID16 - Abnormal: Invalid AppealStatus ("PENDING") throws ValidationException
        var reportId = Guid.NewGuid();
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Report { ReportId = reportId, AppealStatus = "PENDING" });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "PENDING" }));

        Assert.Equal("Invalid AppealStatus. Allowed values are 'APPROVED' or 'REJECTED'.", ex.Message);
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenReportNotFound_ShouldThrowNotFoundException()
    {
        // Service UTCID17 - Abnormal: Report missing in repository -> NotFoundException
        var reportId = Guid.NewGuid();
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Report?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "APPROVED" }));

        Assert.Equal("Report not found.", ex.Message);
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealAlreadyRejected_ShouldThrowBusinessException()
    {
        // Service UTCID18 - Abnormal: Appeal already REJECTED blocks re-review -> BusinessException
        var reportId = Guid.NewGuid();
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Report { ReportId = reportId, AppealStatus = "REJECTED" });

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "APPROVED" }));

        Assert.Contains("already been reviewed (REJECTED)", ex.Message);
        _mockUow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenSaveChangesFails_ShouldPropagateException()
    {
        // Service UTCID19 - Abnormal: Database failure during SaveChangesAsync propagates
        var reportId = Guid.NewGuid();
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Report { ReportId = reportId, AppealStatus = "PENDING" });
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB commit failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "REJECTED" }));

        Assert.Equal("DB commit failed", ex.Message);
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenCancelled_ShouldPropagateOperationCanceledException()
    {
        // Service UTCID20 - Abnormal: Cancelled token throws OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var reportId = Guid.NewGuid();

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ReviewAppealAsync(reportId, Guid.NewGuid(), new ReviewAppealRequestDto { AppealStatus = "APPROVED" }, cts.Token));
    }

    #endregion

    #region ReportRepository.GetReportWithDetailsByIdAsync (UTCID01 - UTCID05)

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    private static User CreateUser(Guid id, string fullName)
    {
        return new User
        {
            UserId = id,
            FullName = fullName,
            Email = $"{id:N}@fitsocial.test",
            RoleCode = RoleConstants.Trainee
        };
    }

    [Fact]
    public async Task Repository_GetReportWithDetailsByIdAsync_ForUserReportWithAppeals_ShouldLoadAllDetails()
    {
        // Repo UTCID01 - Normal: Loads Reporter, ReportedUser, Appeals, AppealMedia, and AppealReviewedByNavigation
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        var reportedUser = CreateUser(Guid.NewGuid(), "Reported");
        var reviewer = CreateUser(Guid.NewGuid(), "Staff");
        context.Users.AddRange(reporter, reportedUser, reviewer);

        var reportId = Guid.NewGuid();
        var appealId = Guid.NewGuid();
        context.Reports.Add(new Report
        {
            ReportId = reportId,
            ReporterId = reporter.UserId,
            ReportedUserId = reportedUser.UserId,
            AppealStatus = "PENDING",
            AppealReviewedBy = reviewer.UserId
        });
        context.Appeals.Add(new Appeal
        {
            AppealId = appealId,
            ReportId = reportId,
            AppealerId = reportedUser.UserId,
            Status = "PENDING",
            AppealMedia = new List<AppealMedium>
            {
                new() { MediaId = Guid.NewGuid(), AppealId = appealId, MediaUrl = "https://cdn/proof.png", MediaType = "IMAGE", SortOrder = 1 }
            }
        });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var loaded = await repo.GetReportWithDetailsByIdAsync(reportId);

        Assert.NotNull(loaded);
        Assert.Equal("Reporter", loaded!.Reporter.FullName);
        Assert.Equal("Reported", loaded.ReportedUser!.FullName);
        Assert.Equal("Staff", loaded.AppealReviewedByNavigation!.FullName);
        var appeal = Assert.Single(loaded.Appeals);
        Assert.Single(appeal.AppealMedia);
    }

    [Fact]
    public async Task Repository_GetReportWithDetailsByIdAsync_ForPostReport_ShouldLoadReportedPostAndAuthor()
    {
        // Repo UTCID02 - Normal: Loads ReportedPost and its Author navigation property
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        var author = CreateUser(Guid.NewGuid(), "Post Author");
        context.Users.AddRange(reporter, author);

        var postId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        context.Posts.Add(new Post { Id = postId, AuthorId = author.UserId, Content = "Sample", PostType = "TEXT" });
        context.Reports.Add(new Report
        {
            ReportId = reportId, ReporterId = reporter.UserId, ReportedPostId = postId, AppealStatus = "PENDING"
        });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var loaded = await repo.GetReportWithDetailsByIdAsync(reportId);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.ReportedPost);
        Assert.Equal("Post Author", loaded.ReportedPost!.Author.FullName);
    }

    [Fact]
    public async Task Repository_GetReportWithDetailsByIdAsync_WhenReportNotFoundOrEmptyGuid_ShouldReturnNull()
    {
        // Repo UTCID03 - Boundary: Non-existent reportId or Guid.Empty returns null
        using var context = CreateInMemoryDbContext();
        var repo = new ReportRepository(context);

        Assert.Null(await repo.GetReportWithDetailsByIdAsync(Guid.NewGuid()));
        Assert.Null(await repo.GetReportWithDetailsByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task Repository_GetReportWithDetailsByIdAsync_WhenNoAppealsOrMedia_ShouldReturnEmptyCollections()
    {
        // Repo UTCID04 - Boundary: Report without Appeals or ReportMedia loads with empty collections
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);
        var reportId = Guid.NewGuid();
        context.Reports.Add(new Report { ReportId = reportId, ReporterId = reporter.UserId, AppealStatus = "PENDING" });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var loaded = await repo.GetReportWithDetailsByIdAsync(reportId);

        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Appeals);
        Assert.Empty(loaded.ReportMedia);
    }

    [Fact]
    public async Task Repository_GetReportWithDetailsByIdAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo UTCID05 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new ReportRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetReportWithDetailsByIdAsync(Guid.NewGuid(), cts.Token));
    }

    #endregion
}

