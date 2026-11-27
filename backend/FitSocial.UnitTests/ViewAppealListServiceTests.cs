using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for View Appeal List feature (Staff/Admin Appeal Management):
/// 1. ReportsController.GetReports(GetReportsQueryDto query, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID04): Staff NameIdentifier 200 OK, Admin "sub" claim 200 OK, CancellationToken forwarding, [Authorize(Roles="ADMIN,STAFF")] attribute check.
///    - Boundary (UTCID05 - UTCID06): Empty/whitespace NameIdentifier -> currentUserId=null, empty appeal list -> 200 OK.
///    - Abnormal (UTCID07 - UTCID10): Service Fail responses (unauthenticated/not found, locked account, unauthorized role, invalid target type) -> 400 BadRequest.
/// 2. ReportService.GetReportsAsync(GetReportsQueryDto query, Guid? currentUserId, CancellationToken cancellationToken)
///    - Normal (UTCID01 - UTCID06): AppealStatus="ALL" with full appeal/AppealMedia mapping, trimmed AppealStatus & SearchTerm, APPROVED/REJECTED resolver mapping, POST target & coach sold package check, query.CurrentUserId fallback, null viewer skip.
///    - Boundary (UTCID07 - UTCID11): Null query default, PageNumber<1 & PageSize<1 clamping, PageSize=100 & 150>100 clamping, PageSize=1 min, empty list / empty Appeals.
///    - Abnormal (UTCID12 - UTCID16): Viewer not found, locked viewer, unauthorized role, invalid TargetType, repository/cancellation exception -> Fail result.
/// 3. ReportRepository.GetPagedReportsAsync(..., appealStatus, ...)
///    - Normal (UTCID01 - UTCID04): appealStatus="ALL" with Appeals/AppealMedia & CreatedAt desc, case-insensitive specific status ("pending"/"APPROVED"), combined TargetType/Date filters, page 2 slicing.
///    - Boundary (UTCID05 - UTCID08): Excludes null and "" AppealStatus when ALL, pageNumber<=0 & pageSize<=0 clamping to 1/10, exact fromDate/toDate inclusive boundaries, zero matches.
///    - Abnormal (UTCID09 - UTCID10): Unknown targetType safe handling, cancelled CancellationToken throws OperationCanceledException.
/// </summary>
public class ViewAppealListServiceTests
{
    #region ReportsController.GetReports (UTCID01 - UTCID10)

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
    public async Task ReportsController_GetReports_WhenStaffQueriesAppeals_ShouldReturn200Ok()
    {
        // Controller UTCID01 - Normal: Staff queries appeal list (AppealStatus="ALL") -> 200 OK
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Staff));

        var query = new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 1, PageSize = 10 };
        var paged = PagedResultDto<ReportListItemDto>.Create(
            new List<ReportListItemDto>
            {
                new() { ReportId = Guid.NewGuid(), AppealStatus = "PENDING", AppealContent = "Please review my post." }
            },
            1, 1, 10);

        mockService.Setup(s => s.GetReportsAsync(query, staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(paged, "Reports retrieved successfully."));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Single(body.Data!.Items);
        Assert.Equal("PENDING", body.Data.Items[0].AppealStatus);
        mockService.Verify(s => s.GetReportsAsync(query, staffId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenAdminUsesSubClaim_ShouldReturn200Ok()
    {
        // Controller UTCID02 - Normal: Admin authenticated via "sub" claim queries PENDING appeals -> 200 OK
        var mockService = new Mock<IReportService>();
        var adminId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim("sub", adminId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Admin));

        var query = new GetReportsQueryDto { AppealStatus = "PENDING" };
        mockService.Setup(s => s.GetReportsAsync(query, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(
                PagedResultDto<ReportListItemDto>.Create(new List<ReportListItemDto>(), 0, 1, 10)));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        mockService.Verify(s => s.GetReportsAsync(query, adminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportsController_GetReports_ShouldForwardCancellationTokenToService()
    {
        // Controller UTCID03 - Normal: Explicit CancellationToken is forwarded to IReportService
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Staff));

        var query = new GetReportsQueryDto { AppealStatus = "ALL" };
        mockService.Setup(s => s.GetReportsAsync(query, staffId, token))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(
                PagedResultDto<ReportListItemDto>.Create(new List<ReportListItemDto>(), 0, 1, 10)));

        var actionResult = await controller.GetReports(query, token);

        Assert.IsType<OkObjectResult>(actionResult);
        mockService.Verify(s => s.GetReportsAsync(query, staffId, token), Times.Once);
    }

    [Fact]
    public void ReportsController_GetReports_HasAuthorizeAdminOrStaffAttribute()
    {
        // Controller UTCID04 - Normal: Endpoint enforces ADMIN or STAFF role authorization
        var method = typeof(ReportsController).GetMethod(nameof(ReportsController.GetReports));
        Assert.NotNull(method);
        var attr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal($"{RoleConstants.Admin},{RoleConstants.Staff}", attr!.Roles);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenNameIdentifierIsWhitespace_ShouldReturnUnauthorized()
    {
        // Controller UTCID05 - Boundary: Whitespace NameIdentifier is rejected with 401 Unauthorized
        var mockService = new Mock<IReportService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "   "),
            new Claim(ClaimTypes.Role, RoleConstants.Staff));

        var query = new GetReportsQueryDto { AppealStatus = "ALL" };

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(actionResult);
        mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenAppealListEmpty_ShouldReturn200OkWithZeroItems()
    {
        // Controller UTCID06 - Boundary: Empty appeal list returns 200 OK with TotalCount = 0
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var query = new GetReportsQueryDto { AppealStatus = "REJECTED" };
        mockService.Setup(s => s.GetReportsAsync(query, staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Ok(
                PagedResultDto<ReportListItemDto>.Create(new List<ReportListItemDto>(), 0, 1, 10)));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Empty(body.Data!.Items);
        Assert.Equal(0, body.Data.TotalCount);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenServiceReturnsUserNotFound_ShouldReturn400BadRequest()
    {
        // Controller UTCID07 - Abnormal: Service Fail("User not found.") returns 400 BadRequest
        var mockService = new Mock<IReportService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(mockService, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var query = new GetReportsQueryDto { AppealStatus = "ALL" };
        mockService.Setup(s => s.GetReportsAsync(query, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("User not found."));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(badRequest.Value);
        Assert.False(body.Success);
        Assert.Equal("User not found.", body.Message);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenViewerAccountLocked_ShouldReturn400BadRequest()
    {
        // Controller UTCID08 - Abnormal: Locked staff viewer -> 400 BadRequest
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(mockService, new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var query = new GetReportsQueryDto { AppealStatus = "ALL" };
        mockService.Setup(s => s.GetReportsAsync(query, staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("Account is locked."));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(badRequest.Value);
        Assert.False(body.Success);
        Assert.Equal("Account is locked.", body.Message);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenViewerUnauthorizedRole_ShouldReturn400BadRequest()
    {
        // Controller UTCID09 - Abnormal: Unauthorized viewer role -> 400 BadRequest
        var mockService = new Mock<IReportService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(mockService, new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

        var query = new GetReportsQueryDto { AppealStatus = "ALL" };
        mockService.Setup(s => s.GetReportsAsync(query, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("Access denied. User is not authorized to view reports."));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.False(Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(badRequest.Value).Success);
    }

    [Fact]
    public async Task ReportsController_GetReports_WhenInvalidTargetType_ShouldReturn400BadRequest()
    {
        // Controller UTCID10 - Abnormal: Invalid TargetType filter -> 400 BadRequest
        var mockService = new Mock<IReportService>();
        var staffId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(mockService, new Claim(ClaimTypes.NameIdentifier, staffId.ToString()));

        var query = new GetReportsQueryDto { AppealStatus = "ALL", TargetType = "INVALID" };
        mockService.Setup(s => s.GetReportsAsync(query, staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<ReportListItemDto>>.Fail("Invalid target type. Allowed values are 'POST' or 'USER'."));

        var actionResult = await controller.GetReports(query, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<ReportListItemDto>>>(badRequest.Value);
        Assert.False(body.Success);
        Assert.Contains("Invalid target type", body.Message);
    }

    #endregion

    #region ReportService.GetReportsAsync - Appeal List (UTCID01 - UTCID16)

    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachProfileRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly ReportService _service;

    public ViewAppealListServiceTests()
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

    private static User CreateViewer(string roleCode = RoleConstants.Staff, bool isLocked = false)
    {
        return new User
        {
            UserId = Guid.NewGuid(),
            FullName = "Moderator",
            Email = "mod@fitsocial.com",
            RoleCode = roleCode,
            IsLocked = isLocked
        };
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenAppealStatusIsAll_ShouldMapAppealsAndSortedAppealMedia()
    {
        // Service UTCID01 - Normal: AppealStatus="ALL" maps appeal details and AppealMedia sorted by SortOrder with MediaFor="APPEAL"
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);

        var reporter = new User { UserId = Guid.NewGuid(), FullName = "Reporter Lee", Email = "rep@fitsocial.com" };
        var reportedUser = new User { UserId = Guid.NewGuid(), FullName = "Appealer Kim", Email = "kim@fitsocial.com", RoleCode = RoleConstants.Trainee };
        var appealId = Guid.NewGuid();
        var appealedAt = DateTime.UtcNow.AddHours(-2);

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = reporter.UserId,
            Reporter = reporter,
            ReportedUserId = reportedUser.UserId,
            ReportedUser = reportedUser,
            Reason = "Spam",
            Status = "RESOLVED",
            AppealStatus = "PENDING",
            AppealContent = "This was a genuine workout tip, not spam.",
            AppealedAt = appealedAt,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Appeals = new List<Appeal>
            {
                new()
                {
                    AppealId = appealId,
                    AppealerId = reportedUser.UserId,
                    Status = "PENDING",
                    AppealMedia = new List<AppealMedium>
                    {
                        new() { MediaId = Guid.NewGuid(), AppealId = appealId, MediaUrl = "https://cdn/2.png", MediaType = "IMAGE", SortOrder = 2 },
                        new() { MediaId = Guid.NewGuid(), AppealId = appealId, MediaUrl = "https://cdn/1.png", MediaType = "IMAGE", SortOrder = 1 }
                    }
                }
            }
        };

        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        var query = new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 1, PageSize = 10 };
        var result = await _service.GetReportsAsync(query, staff.UserId);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("PENDING", item.AppealStatus);
        Assert.Equal("This was a genuine workout tip, not spam.", item.AppealContent);
        Assert.Equal(appealedAt, item.AppealedAt);
        Assert.Equal(2, item.AppealMedia.Count);
        Assert.Equal("https://cdn/1.png", item.AppealMedia[0].MediaUrl);
        Assert.Equal("https://cdn/2.png", item.AppealMedia[1].MediaUrl);
        Assert.All(item.AppealMedia, m => Assert.Equal("APPEAL", m.MediaFor));
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenAdminFiltersTrimmedAppealStatusAndSearchTerm_ShouldForwardTrimmedValues()
    {
        // Service UTCID02 - Normal: Admin (case-insensitive) passes trimmed AppealStatus & SearchTerm to repository
        var admin = CreateViewer("admin");
        _mockUserRepo.Setup(u => u.GetByIdAsync(admin.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, "misunderstanding", 1, 10, null, "PENDING", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var query = new GetReportsQueryDto { AppealStatus = "  PENDING  ", SearchTerm = "  misunderstanding  " };
        var result = await _service.GetReportsAsync(query, admin.UserId);

        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, "misunderstanding", 1, 10, null, "PENDING", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenAppealIsApprovedOrRejected_ShouldMapResolverFields()
    {
        // Service UTCID03 - Normal: Maps AppealReviewedBy, AppealResolverName, AppealReviewedAt, AppealReviewNote
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);

        var reviewer = new User { UserId = Guid.NewGuid(), FullName = "Admin Reviewer", Email = "rev@fitsocial.com" };
        var reviewedAt = DateTime.UtcNow.AddMinutes(-30);
        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedUserId = Guid.NewGuid(),
            Status = "DISMISSED",
            AppealStatus = "APPROVED",
            AppealContent = "Unfair report",
            AppealReviewedBy = reviewer.UserId,
            AppealReviewedByNavigation = reviewer,
            AppealReviewedAt = reviewedAt,
            AppealReviewNote = "Accepted evidence, warning reverted."
        };

        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, "APPROVED", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "APPROVED" }, staff.UserId);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("APPROVED", item.AppealStatus);
        Assert.Equal(reviewer.UserId, item.AppealReviewedBy);
        Assert.Equal("Admin Reviewer", item.AppealResolverName);
        Assert.Equal(reviewedAt, item.AppealReviewedAt);
        Assert.Equal("Accepted evidence, warning reverted.", item.AppealReviewNote);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenPostAppealTargetsCoach_ShouldCheckCoachSoldPackages()
    {
        // Service UTCID04 - Normal: Post appeal with Coach author checks HasSoldTrainingPackageAsync and maps POST fields
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);

        var coach = new User { UserId = Guid.NewGuid(), FullName = "Coach Minh", RoleCode = RoleConstants.Coach, WarningCount = 2 };
        var post = new Post { Id = Guid.NewGuid(), AuthorId = coach.UserId, Author = coach, Content = "Workout post", IsDeleted = false };
        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedPostId = post.Id,
            ReportedPost = post,
            Status = "PENDING",
            AppealStatus = "PENDING"
        };

        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, "POST", null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));
        _mockOrderRepo.Setup(o => o.GetCoachIdsWithSoldPackagesAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(coach.UserId)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { coach.UserId });

        var query = new GetReportsQueryDto { AppealStatus = "ALL", TargetType = " post " };
        var result = await _service.GetReportsAsync(query, staff.UserId);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Items);
        Assert.Equal("POST", item.TargetType);
        Assert.Equal(coach.UserId, item.ReportedUserId);
        Assert.True(item.HasSoldTrainingPackage);
        _mockOrderRepo.Verify(o => o.GetCoachIdsWithSoldPackagesAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(coach.UserId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenCurrentUserIdParamIsNull_ShouldFallbackToQueryCurrentUserId()
    {
        // Service UTCID05 - Normal: Uses query.CurrentUserId when currentUserId parameter is null
        var admin = CreateViewer(RoleConstants.Admin);
        _mockUserRepo.Setup(u => u.GetByIdAsync(admin.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var query = new GetReportsQueryDto { AppealStatus = "ALL", CurrentUserId = admin.UserId };
        var result = await _service.GetReportsAsync(query, null);

        Assert.True(result.Success);
        _mockUserRepo.Verify(u => u.GetByIdAsync(admin.UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenNoViewerIdProvided_ShouldReturnUnauthenticatedFail()
    {
        // Service UTCID06 - Abnormal: When both currentUserId and query.CurrentUserId are null, rejects unauthenticated access
        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, (Guid?)null);

        Assert.False(result.Success);
        Assert.Equal("User is not authenticated.", result.Message);
        _mockUserRepo.Verify(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenQueryIsNull_ShouldUseDefaultPagingSafely()
    {
        // Service UTCID07 - Boundary: Null query falls back to new GetReportsQueryDto() (PageNumber=1, PageSize=10)
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var result = await _service.GetReportsAsync(null!, staff.UserId);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.PageNumber);
        Assert.Equal(10, result.Data.PageSize);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenPageNumberOrPageSizeBelow1_ShouldClampTo1And10()
    {
        // Service UTCID08 - Boundary: PageNumber <= 0 clamped to 1; PageSize <= 0 clamped to 10
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var result = await _service.GetReportsAsync(
            new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 0, PageSize = -5 }, staff.UserId);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.PageNumber);
        Assert.Equal(10, result.Data.PageSize);
        _mockReportRepo.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenPageSizeAtOrAbove100_ShouldRespectMax100Boundary()
    {
        // Service UTCID09 - Boundary: PageSize = 100 stays 100; PageSize = 150 clamped to 100
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 100, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var exactMax = await _service.GetReportsAsync(
            new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 1, PageSize = 100 }, staff.UserId);
        var overMax = await _service.GetReportsAsync(
            new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 1, PageSize = 150 }, staff.UserId);

        Assert.True(exactMax.Success);
        Assert.Equal(100, exactMax.Data!.PageSize);
        Assert.True(overMax.Success);
        Assert.Equal(100, overMax.Data!.PageSize);
        _mockReportRepo.Verify(r => r.GetPagedReportsAsync(
            null, null, null, null, null, null, 1, 100, null, "ALL", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenPageSizeIsMinimum1_ShouldPassPageSize1()
    {
        // Service UTCID10 - Boundary: Minimum valid PageSize = 1 is preserved
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 1, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report>(), 0));

        var result = await _service.GetReportsAsync(
            new GetReportsQueryDto { AppealStatus = "ALL", PageNumber = 1, PageSize = 1 }, staff.UserId);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.PageSize);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenReportHasEmptyAppealsCollection_ShouldMapEmptyAppealMedia()
    {
        // Service UTCID11 - Boundary: Report with AppealStatus set and empty Appeals collection maps empty AppealMedia list
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);

        var report = new Report
        {
            ReportId = Guid.NewGuid(),
            ReporterId = Guid.NewGuid(),
            ReportedUserId = Guid.NewGuid(),
            AppealStatus = "PENDING",
            AppealContent = "Legacy appeal without AppealMedia rows",
            Appeals = new List<Appeal>()
        };
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                null, null, null, null, null, null, 1, 10, null, "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Report> { report }, 1));

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, staff.UserId);

        Assert.True(result.Success);
        var item = Assert.Single(result.Data!.Items);
        Assert.Empty(item.AppealMedia);
        Assert.Equal("Legacy appeal without AppealMedia rows", item.AppealContent);
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenViewerNotFound_ShouldReturnFailResult()
    {
        // Service UTCID12 - Abnormal: Non-existent viewerId -> Fail("User not found.")
        var missingId = Guid.NewGuid();
        _mockUserRepo.Setup(u => u.GetByIdAsync(missingId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, missingId);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenViewerIsLocked_ShouldReturnFailResult()
    {
        // Service UTCID13 - Abnormal: Locked staff account -> Fail("Account is locked.")
        var locked = CreateViewer(RoleConstants.Staff, isLocked: true);
        _mockUserRepo.Setup(u => u.GetByIdAsync(locked.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(locked);

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, locked.UserId);

        Assert.False(result.Success);
        Assert.Equal("Account is locked.", result.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenViewerIsCoachOrTrainee_ShouldReturnAccessDenied()
    {
        // Service UTCID14 - Abnormal: Non-staff/non-admin role -> Fail("Access denied...")
        var coach = CreateViewer(RoleConstants.Coach);
        _mockUserRepo.Setup(u => u.GetByIdAsync(coach.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(coach);

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, coach.UserId);

        Assert.False(result.Success);
        Assert.Equal("Access denied. User is not authorized to view reports.", result.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenTargetTypeInvalid_ShouldReturnFailResult()
    {
        // Service UTCID15 - Abnormal: Invalid TargetType ("MESSAGE") -> Fail("Invalid target type...")
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);

        var result = await _service.GetReportsAsync(
            new GetReportsQueryDto { AppealStatus = "ALL", TargetType = "MESSAGE" }, staff.UserId);

        Assert.False(result.Success);
        Assert.Equal("Invalid target type. Allowed values are 'POST' or 'USER'.", result.Message);
        _mockReportRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReportService_GetReportsAsync_WhenRepositoryThrowsException_ShouldCatchAndReturnFailResult()
    {
        // Service UTCID16 - Abnormal: Database failure is caught and wrapped in ApiResponseDto.Fail
        var staff = CreateViewer(RoleConstants.Staff);
        _mockUserRepo.Setup(u => u.GetByIdAsync(staff.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        _mockReportRepo.Setup(r => r.GetPagedReportsAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        var result = await _service.GetReportsAsync(new GetReportsQueryDto { AppealStatus = "ALL" }, staff.UserId);

        Assert.False(result.Success);
        Assert.Equal("Error retrieving reports: Database connection lost", result.Message);
    }

    #endregion

    #region ReportRepository.GetPagedReportsAsync - Appeal List (UTCID01 - UTCID10)

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    private static User CreateUser(Guid id, string fullName, string role = RoleConstants.Trainee)
    {
        return new User
        {
            UserId = id,
            FullName = fullName,
            Email = $"{id:N}@fitsocial.test",
            RoleCode = role
        };
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WithAppealStatusAll_ShouldReturnOnlyAppealedReportsSortedDescWithMedia()
    {
        // Repo UTCID01 - Normal: appealStatus="ALL" returns reports with non-empty AppealStatus, ordered CreatedAt desc, with Appeals & AppealMedia
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        var appealer = CreateUser(Guid.NewGuid(), "Appealer");
        context.Users.AddRange(reporter, appealer);

        var olderPendingId = Guid.NewGuid();
        var newerApprovedId = Guid.NewGuid();
        var appealId = Guid.NewGuid();

        context.Reports.AddRange(
            new Report
            {
                ReportId = olderPendingId, ReporterId = reporter.UserId, ReportedUserId = appealer.UserId,
                Status = "RESOLVED", AppealStatus = "PENDING", CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
            },
            new Report
            {
                ReportId = newerApprovedId, ReporterId = reporter.UserId, ReportedUserId = appealer.UserId,
                Status = "DISMISSED", AppealStatus = "APPROVED", CreatedAt = new DateTime(2026, 5, 5, 8, 0, 0, DateTimeKind.Utc)
            },
            new Report
            {
                ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, ReportedUserId = appealer.UserId,
                Status = "PENDING", AppealStatus = null, CreatedAt = new DateTime(2026, 5, 6, 8, 0, 0, DateTimeKind.Utc)
            });

        context.Appeals.Add(new Appeal
        {
            AppealId = appealId, ReportId = newerApprovedId, AppealerId = appealer.UserId, Status = "APPROVED",
            AppealMedia = new List<AppealMedium>
            {
                new() { MediaId = Guid.NewGuid(), AppealId = appealId, MediaUrl = "https://cdn/proof.jpg", MediaType = "IMAGE", SortOrder = 1 }
            }
        });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "ALL");

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Equal(newerApprovedId, items[0].ReportId);
        Assert.Single(items[0].Appeals);
        Assert.Single(items[0].Appeals.First().AppealMedia);
        Assert.Equal(olderPendingId, items[1].ReportId);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WithSpecificAppealStatusCaseInsensitive_ShouldFilterAccurately()
    {
        // Repo UTCID02 - Normal: Specific appealStatus ("pending" / "REJECTED") matches case-insensitively
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);

        context.Reports.AddRange(
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "APPROVED", CreatedAt = DateTime.UtcNow },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "REJECTED", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (pendingItems, pendingCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "pending");
        var (rejectedItems, rejectedCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "REJECTED");

        Assert.Equal(1, pendingCount);
        Assert.Equal("PENDING", Assert.Single(pendingItems).AppealStatus);
        Assert.Equal(1, rejectedCount);
        Assert.Equal("REJECTED", Assert.Single(rejectedItems).AppealStatus);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WithCombinedTargetTypeAndStatus_ShouldFilterAppeals()
    {
        // Repo UTCID03 - Normal: Combines appealStatus="ALL" with targetType ("POST" vs "USER")
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        var author = CreateUser(Guid.NewGuid(), "Author");
        context.Users.AddRange(reporter, author);

        var postId = Guid.NewGuid();
        context.Posts.Add(new Post { Id = postId, AuthorId = author.UserId, Content = "Post", PostType = "TEXT" });
        context.Reports.AddRange(
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, ReportedPostId = postId, AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, ReportedUserId = author.UserId, ReportedPostId = null, AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (postAppeals, postCount) = await repo.GetPagedReportsAsync(null, "POST", null, null, null, null, 1, 10, null, "ALL");
        var (userAppeals, userCount) = await repo.GetPagedReportsAsync(null, "USER", null, null, null, null, 1, 10, null, "ALL");

        Assert.Equal(1, postCount);
        Assert.Equal(postId, Assert.Single(postAppeals).ReportedPostId);
        Assert.Equal(1, userCount);
        Assert.Null(Assert.Single(userAppeals).ReportedPostId);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_ShouldPaginateCorrectlyAcrossPages()
    {
        // Repo UTCID04 - Normal: Page 2 with pageSize = 2 on 5 appealed reports returns items 3 & 4 in CreatedAt desc order
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);

        for (int i = 1; i <= 5; i++)
        {
            context.Reports.Add(new Report
            {
                ReportId = Guid.NewGuid(),
                ReporterId = reporter.UserId,
                Reason = $"Appeal #{i}",
                AppealStatus = "PENDING",
                CreatedAt = new DateTime(2026, 5, i, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 2, 2, null, "ALL");

        Assert.Equal(5, totalCount);
        Assert.Equal(2, items.Count);
        Assert.Equal("Appeal #3", items[0].Reason);
        Assert.Equal("Appeal #2", items[1].Reason);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WhenAppealStatusIsAll_ShouldExcludeNullAndEmptyAppealStatus()
    {
        // Repo UTCID05 - Boundary: appealStatus="ALL" excludes both null and "" AppealStatus
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);

        context.Reports.AddRange(
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = null, CreatedAt = DateTime.UtcNow },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "", CreatedAt = DateTime.UtcNow },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "ALL");

        Assert.Equal(1, totalCount);
        Assert.Equal("PENDING", Assert.Single(items).AppealStatus);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WhenPageNumberOrPageSizeBelow1_ShouldClampTo1And10()
    {
        // Repo UTCID06 - Boundary: pageNumber <= 0 and pageSize <= 0 clamp to 1 and 10
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);

        for (int i = 0; i < 12; i++)
        {
            context.Reports.Add(new Report
            {
                ReportId = Guid.NewGuid(), ReporterId = reporter.UserId,
                AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 0, 0, null, "ALL");

        Assert.Equal(12, totalCount);
        Assert.Equal(10, items.Count);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_DateRangeInclusiveBoundaries_ShouldIncludeExactBoundsAndExcludeOutside()
    {
        // Repo UTCID07 - Boundary: Exact fromDate & toDate are inclusive; outside by 1 second excluded
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);

        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 10, 23, 59, 59, DateTimeKind.Utc);

        context.Reports.AddRange(
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, Reason = "AtFrom", AppealStatus = "PENDING", CreatedAt = from },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, Reason = "AtTo", AppealStatus = "PENDING", CreatedAt = to },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, Reason = "Before", AppealStatus = "PENDING", CreatedAt = from.AddSeconds(-1) },
            new Report { ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, Reason = "After", AppealStatus = "PENDING", CreatedAt = to.AddSeconds(1) });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, from, to, null, 1, 10, null, "ALL");

        Assert.Equal(2, totalCount);
        Assert.Contains(items, r => r.Reason == "AtFrom");
        Assert.Contains(items, r => r.Reason == "AtTo");
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WhenNoAppealsMatch_ShouldReturnEmptyAndZeroCount()
    {
        // Repo UTCID08 - Boundary: Empty database or no matching appeals returns empty list and TotalCount = 0
        using var context = CreateInMemoryDbContext();
        var repo = new ReportRepository(context);

        var (items, totalCount) = await repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "ALL");

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WhenUnknownTargetTypePassedDirectly_ShouldIgnoreWithoutThrowing()
    {
        // Repo UTCID09 - Abnormal: Unrecognized targetType in repository ignores target filter safely
        using var context = CreateInMemoryDbContext();
        var reporter = CreateUser(Guid.NewGuid(), "Reporter");
        context.Users.Add(reporter);
        context.Reports.Add(new Report
        {
            ReportId = Guid.NewGuid(), ReporterId = reporter.UserId, AppealStatus = "PENDING", CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new ReportRepository(context);
        var (items, totalCount) = await repo.GetPagedReportsAsync(null, "UNKNOWN", null, null, null, null, 1, 10, null, "ALL");

        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    [Fact]
    public async Task Repository_GetPagedReportsAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo UTCID10 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new ReportRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetPagedReportsAsync(null, null, null, null, null, null, 1, 10, null, "ALL", cts.Token));
    }

    #endregion
}
