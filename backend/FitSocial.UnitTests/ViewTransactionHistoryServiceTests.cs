using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Transactions;
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
/// Unit test suite for View Transaction History feature:
/// 1. TransactionsController.GetMyTransactionHistory(TransactionFilterDto filter)
///    - Normal (UTCID01 - UTCID05): Trainee NameIdentifier 200 OK, Coach NameIdentifier 200 OK, "sub" claim fallback 200 OK, custom role claim detection, non-coach role mapped to TRAINEE.
///    - Boundary (UTCID06 - UTCID07): Empty/whitespace NameIdentifier -> 401, empty paged result -> 200 OK.
///    - Abnormal (UTCID08 - UTCID11): Unauthenticated 401, invalid NameIdentifier GUID 401, invalid "sub" GUID 401, service exception propagation.
/// 2. TransactionService.GetMyTransactionsAsync(Guid userId, string userRole, TransactionFilterDto filter)
///    - Normal (UTCID01 - UTCID04): Trainee mapping, Coach mapping, filter forwarding & paging metadata, empty list.
///    - Boundary (UTCID05 - UTCID07): Single item page (PageSize=1) & PageSize=100, PageSize=0 safe TotalPages, Guid.Empty / empty role.
///    - Abnormal (UTCID08 - UTCID10): Null filter throws NullReferenceException, DB exception propagation, OperationCanceledException propagation.
/// 3. TransactionRepository.GetUserTransactionsAsync(...)
///    - Normal (UTCID01 - UTCID06): Trainee orders & refunds, Coach sold/activation/refund/payout, package CoachId & coach-as-buyer, type filter, date range filter, null fallbacks.
///    - Boundary (UTCID07 - UTCID13): Lower pageIndex/pageSize clamping, upper pageSize=100 clamping, VN UTC+7 date boundaries, out-of-range pageIndex, zero records, null amount fallbacks, unknown type filter.
///    - Abnormal (UTCID14 - UTCID15): Null/empty userRole safe fallback, cancelled CancellationToken throws OperationCanceledException.
/// </summary>
public class ViewTransactionHistoryServiceTests
{
    #region TransactionsController.GetMyTransactionHistory (UTCID01 - UTCID11)

    private static TransactionsController CreateControllerWithClaims(Mock<ITransactionService> mockService, params Claim[] claims)
    {
        var controller = new TransactionsController(mockService.Object);
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
    public async Task TransactionsController_GetMyTransactionHistory_WhenTraineeAuthenticated_ShouldReturn200OkWithTraineeRole()
    {
        // Controller UTCID01 - Normal: Authenticated trainee with NameIdentifier claim -> calls service with "TRAINEE" and returns 200 OK
        var mockService = new Mock<ITransactionService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var filter = new TransactionFilterDto { PageIndex = 1, PageSize = 10 };
        var expected = PagedResultDto<TransactionHistoryDto>.Create(
            new List<TransactionHistoryDto>
            {
                new() { TransactionId = Guid.NewGuid(), Amount = -1500000m, Type = "PACKAGE_PURCHASE", Status = "PAID", Currency = "VND" }
            },
            1, 1, 10);

        mockService.Setup(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter))
            .ReturnsAsync(expected);

        var actionResult = await controller.GetMyTransactionHistory(filter);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(okResult.Value);
        Assert.True(body.Success);
        Assert.NotNull(body.Data);
        Assert.Single(body.Data.Items);
        Assert.Equal(-1500000m, body.Data.Items[0].Amount);
        mockService.Verify(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter), Times.Once);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenCoachAuthenticated_ShouldReturn200OkWithCoachRole()
    {
        // Controller UTCID02 - Normal: Authenticated coach -> calls service with "COACH" and returns 200 OK
        var mockService = new Mock<ITransactionService>();
        var coachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var filter = new TransactionFilterDto { Type = "PAYOUT", PageIndex = 1, PageSize = 20 };
        var expected = PagedResultDto<TransactionHistoryDto>.Create(
            new List<TransactionHistoryDto>
            {
                new() { TransactionId = Guid.NewGuid(), Amount = 5000000m, Type = "PAYOUT", Status = "PROCESSED", Currency = "VND" }
            },
            1, 1, 20);

        mockService.Setup(s => s.GetMyTransactionsAsync(coachId, "COACH", filter))
            .ReturnsAsync(expected);

        var actionResult = await controller.GetMyTransactionHistory(filter);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Single(body.Data!.Items);
        Assert.Equal("PAYOUT", body.Data.Items[0].Type);
        mockService.Verify(s => s.GetMyTransactionsAsync(coachId, "COACH", filter), Times.Once);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenSubClaimUsed_ShouldReturn200Ok()
    {
        // Controller UTCID03 - Normal: Falls back to "sub" claim when ClaimTypes.NameIdentifier is absent
        var mockService = new Mock<ITransactionService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var filter = new TransactionFilterDto();
        mockService.Setup(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter))
            .ReturnsAsync(PagedResultDto<TransactionHistoryDto>.Create(new List<TransactionHistoryDto>(), 0, 1, 10));

        var actionResult = await controller.GetMyTransactionHistory(filter);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(okResult.Value);
        Assert.True(body.Success);
        mockService.Verify(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter), Times.Once);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenCustomLowercaseRoleClaimIsCoach_ShouldDetectCoachRole()
    {
        // Controller UTCID04 - Normal: Custom claim ending in "role" with value "coach" (case-insensitive) is recognized as "COACH"
        var mockService = new Mock<ITransactionService>();
        var coachId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim("custom_role", "coach"));

        var filter = new TransactionFilterDto();
        mockService.Setup(s => s.GetMyTransactionsAsync(coachId, "COACH", filter))
            .ReturnsAsync(PagedResultDto<TransactionHistoryDto>.Create(new List<TransactionHistoryDto>(), 0, 1, 10));

        var actionResult = await controller.GetMyTransactionHistory(filter);

        Assert.IsType<OkObjectResult>(actionResult);
        mockService.Verify(s => s.GetMyTransactionsAsync(coachId, "COACH", filter), Times.Once);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenNonCoachRoleClaim_ShouldDefaultToTraineeRole()
    {
        // Controller UTCID05 - Normal: Non-coach role (e.g. ADMIN or absent role) defaults to "TRAINEE"
        var mockService = new Mock<ITransactionService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Admin));

        var filter = new TransactionFilterDto();
        mockService.Setup(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter))
            .ReturnsAsync(PagedResultDto<TransactionHistoryDto>.Create(new List<TransactionHistoryDto>(), 0, 1, 10));

        var actionResult = await controller.GetMyTransactionHistory(filter);

        Assert.IsType<OkObjectResult>(actionResult);
        mockService.Verify(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter), Times.Once);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenNameIdentifierIsEmptyOrWhitespace_ShouldReturn401Unauthorized()
    {
        // Controller UTCID06 - Boundary: Empty or whitespace NameIdentifier fails Guid.TryParse -> 401 Unauthorized
        var mockService = new Mock<ITransactionService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "   "),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var actionResult = await controller.GetMyTransactionHistory(new TransactionFilterDto());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Invalid token or user ID", body.Message);
        mockService.Verify(s => s.GetMyTransactionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TransactionFilterDto>()), Times.Never);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenUserHasNoTransactions_ShouldReturn200OkWithEmptyList()
    {
        // Controller UTCID07 - Boundary: Empty paged result returns 200 OK with 0 items
        var mockService = new Mock<ITransactionService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var filter = new TransactionFilterDto();
        mockService.Setup(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter))
            .ReturnsAsync(PagedResultDto<TransactionHistoryDto>.Create(new List<TransactionHistoryDto>(), 0, 1, 10));

        var actionResult = await controller.GetMyTransactionHistory(filter);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(okResult.Value);
        Assert.True(body.Success);
        Assert.Empty(body.Data!.Items);
        Assert.Equal(0, body.Data.TotalCount);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Controller UTCID08 - Abnormal: Missing user claims -> 401 Unauthorized
        var mockService = new Mock<ITransactionService>();
        var controller = CreateControllerWithClaims(mockService);

        var actionResult = await controller.GetMyTransactionHistory(new TransactionFilterDto());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Invalid token or user ID", body.Message);
        mockService.Verify(s => s.GetMyTransactionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TransactionFilterDto>()), Times.Never);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenNameIdentifierIsInvalidGuid_ShouldReturn401Unauthorized()
    {
        // Controller UTCID09 - Abnormal: Malformed non-GUID NameIdentifier -> 401 Unauthorized
        var mockService = new Mock<ITransactionService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim(ClaimTypes.Role, RoleConstants.Coach));

        var actionResult = await controller.GetMyTransactionHistory(new TransactionFilterDto());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var body = Assert.IsType<ApiResponseDto<PagedResultDto<TransactionHistoryDto>>>(unauthorized.Value);
        Assert.False(body.Success);
        Assert.Equal("Invalid token or user ID", body.Message);
        mockService.Verify(s => s.GetMyTransactionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TransactionFilterDto>()), Times.Never);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenSubClaimIsInvalidGuid_ShouldReturn401Unauthorized()
    {
        // Controller UTCID10 - Abnormal: Absent NameIdentifier and malformed "sub" claim -> 401 Unauthorized
        var mockService = new Mock<ITransactionService>();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim("sub", "bad-sub-id"));

        var actionResult = await controller.GetMyTransactionHistory(new TransactionFilterDto());

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        mockService.Verify(s => s.GetMyTransactionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<TransactionFilterDto>()), Times.Never);
    }

    [Fact]
    public async Task TransactionsController_GetMyTransactionHistory_WhenServiceThrowsException_ShouldPropagateException()
    {
        // Controller UTCID11 - Abnormal: Unexpected exception from service propagates to global exception middleware
        var mockService = new Mock<ITransactionService>();
        var userId = Guid.NewGuid();
        var controller = CreateControllerWithClaims(
            mockService,
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.Trainee));

        var filter = new TransactionFilterDto();
        mockService.Setup(s => s.GetMyTransactionsAsync(userId, "TRAINEE", filter))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.GetMyTransactionHistory(filter));

        Assert.Equal("Database unavailable", ex.Message);
    }

    #endregion

    #region TransactionService.GetMyTransactionsAsync (UTCID01 - UTCID10)

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_ForTrainee_ShouldMapAllFieldsAndPagingCorrectly()
    {
        // Service UTCID01 - Normal: Maps TransactionRecord items for trainee to TransactionHistoryDto with paging metadata
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();
        var tx1Id = Guid.NewGuid();
        var tx2Id = Guid.NewGuid();
        var date1 = new DateTime(2026, 5, 10, 8, 0, 0, DateTimeKind.Utc);
        var date2 = new DateTime(2026, 5, 8, 8, 0, 0, DateTimeKind.Utc);

        var records = new List<TransactionRecord>
        {
            new()
            {
                TransactionId = tx1Id, Date = date1, Amount = 500000m, Currency = "VND",
                Type = "REFUND", Status = "APPROVED", Description = "Refund received: Muscle Gain", ReferenceId = "REF-001"
            },
            new()
            {
                TransactionId = tx2Id, Date = date2, Amount = -1500000m, Currency = "VND",
                Type = "PACKAGE_PURCHASE", Status = "PAID", Description = "Package purchased: Muscle Gain", ReferenceId = "PAY-999"
            }
        };

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((records, 2));

        var result = await service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto());

        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(tx1Id, result.Items[0].TransactionId);
        Assert.Equal(date1, result.Items[0].Date);
        Assert.Equal(500000m, result.Items[0].Amount);
        Assert.Equal("VND", result.Items[0].Currency);
        Assert.Equal("REFUND", result.Items[0].Type);
        Assert.Equal("APPROVED", result.Items[0].Status);
        Assert.Equal("Refund received: Muscle Gain", result.Items[0].Description);
        Assert.Equal("REF-001", result.Items[0].ReferenceId);
        Assert.Equal(tx2Id, result.Items[1].TransactionId);
        Assert.Equal(-1500000m, result.Items[1].Amount);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_ForCoach_ShouldMapMixedTransactionTypes()
    {
        // Service UTCID02 - Normal: Maps coach transactions (PACKAGE_PURCHASE +, COACH_ACTIVATION -, REFUND -, PAYOUT +)
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var coachId = Guid.NewGuid();

        var records = new List<TransactionRecord>
        {
            new() { TransactionId = Guid.NewGuid(), Date = DateTime.UtcNow, Amount = 4000000m, Type = "PAYOUT", Status = "PROCESSED", Description = "System payout", ReferenceId = "PO-1" },
            new() { TransactionId = Guid.NewGuid(), Date = DateTime.UtcNow.AddDays(-1), Amount = 2000000m, Type = "PACKAGE_PURCHASE", Status = "COMPLETED", Description = "Package sold: HIIT", ReferenceId = "GW-2" },
            new() { TransactionId = Guid.NewGuid(), Date = DateTime.UtcNow.AddDays(-2), Amount = -300000m, Type = "COACH_ACTIVATION", Status = "PAID", Description = "Coach subscription activation / renewal", ReferenceId = "GW-3" }
        };

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                coachId, "COACH", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((records, 3));

        var result = await service.GetMyTransactionsAsync(coachId, "COACH", new TransactionFilterDto());

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal("PAYOUT", result.Items[0].Type);
        Assert.Equal(4000000m, result.Items[0].Amount);
        Assert.Equal("PACKAGE_PURCHASE", result.Items[1].Type);
        Assert.Equal("COACH_ACTIVATION", result.Items[2].Type);
        Assert.Equal(-300000m, result.Items[2].Amount);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_ShouldForwardFilterParametersAndComputePagingFlags()
    {
        // Service UTCID03 - Normal: Forwards FromDate, ToDate, Type, PageIndex=2, PageSize=5 and computes HasPreviousPage & HasNextPage
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();
        var from = new DateTime(2026, 4, 1);
        var to = new DateTime(2026, 4, 30);
        var filter = new TransactionFilterDto
        {
            FromDate = from,
            ToDate = to,
            Type = "PACKAGE_PURCHASE",
            PageIndex = 2,
            PageSize = 5
        };

        var records = Enumerable.Range(1, 5)
            .Select(i => new TransactionRecord { TransactionId = Guid.NewGuid(), Amount = -100000m * i, Type = "PACKAGE_PURCHASE" })
            .ToList();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", from, to, "PACKAGE_PURCHASE", 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((records, 12));

        var result = await service.GetMyTransactionsAsync(userId, "TRAINEE", filter);

        Assert.Equal(12, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
        Assert.Equal(5, result.Items.Count);
        mockRepo.Verify(r => r.GetUserTransactionsAsync(
            userId, "TRAINEE", from, to, "PACKAGE_PURCHASE", 2, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenRepositoryReturnsEmpty_ShouldReturnEmptyPagedResult()
    {
        // Service UTCID04 - Normal: Repository returns 0 records -> returns empty Items with TotalCount = 0, TotalPages = 0
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<TransactionRecord>(), 0));

        var result = await service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenPageSizeAtBoundaries1And100_ShouldComputePagesAccurately()
    {
        // Service UTCID05 - Boundary: PageSize = 1 (minimum) and PageSize = 100 (maximum) boundaries
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { new TransactionRecord { TransactionId = Guid.NewGuid() } }, 1));

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { new TransactionRecord { TransactionId = Guid.NewGuid() } }, 101));

        var minPageResult = await service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto { PageIndex = 1, PageSize = 1 });
        var maxPageResult = await service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto { PageIndex = 1, PageSize = 100 });

        Assert.Equal(1, minPageResult.TotalPages);
        Assert.False(minPageResult.HasNextPage);
        Assert.Equal(2, maxPageResult.TotalPages);
        Assert.True(maxPageResult.HasNextPage);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenPageSizeIsZero_ShouldClampTo10AndComputeTotalPagesSafely()
    {
        // Service UTCID06 - Boundary: PageSize = 0 clamps to 10 in service before repository & PagedResultDto
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<TransactionRecord>(), 5));

        var result = await service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto { PageIndex = 1, PageSize = 0 });

        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        mockRepo.Verify(r => r.GetUserTransactionsAsync(
            userId, "TRAINEE", null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenUserIdEmptyOrRoleEmpty_ShouldPassThroughSafely()
    {
        // Service UTCID07 - Boundary: Guid.Empty userId and empty role forwarded to repository without throwing
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                Guid.Empty, string.Empty, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<TransactionRecord>(), 0));

        var result = await service.GetMyTransactionsAsync(Guid.Empty, string.Empty, new TransactionFilterDto());

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        mockRepo.Verify(r => r.GetUserTransactionsAsync(
            Guid.Empty, string.Empty, null, null, null, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenFilterIsNull_ShouldThrowNullReferenceException()
    {
        // Service UTCID08 - Abnormal: Null filter throws NullReferenceException when accessing filter properties
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            service.GetMyTransactionsAsync(Guid.NewGuid(), "TRAINEE", null!));
        mockRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenRepositoryThrowsDatabaseException_ShouldPropagateException()
    {
        // Service UTCID09 - Abnormal: Repository database failure propagates
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "TRAINEE", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQL timeout"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetMyTransactionsAsync(userId, "TRAINEE", new TransactionFilterDto()));

        Assert.Equal("SQL timeout", ex.Message);
    }

    [Fact]
    public async Task TransactionService_GetMyTransactionsAsync_WhenOperationCancelled_ShouldPropagateOperationCanceledException()
    {
        // Service UTCID10 - Abnormal: OperationCanceledException from repository propagates
        var mockRepo = new Mock<ITransactionRepository>();
        var service = new TransactionService(mockRepo.Object);
        var userId = Guid.NewGuid();

        mockRepo.Setup(r => r.GetUserTransactionsAsync(
                userId, "COACH", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetMyTransactionsAsync(userId, "COACH", new TransactionFilterDto()));
    }

    #endregion

    #region TransactionRepository.GetUserTransactionsAsync (UTCID01 - UTCID15)

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FitSocialDbContext(options);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_ForTrainee_ShouldReturnOrdersAndRefundsSortedDescAndExcludePayouts()
    {
        // Repo UTCID01 - Normal: Trainee receives own paid orders (preferring SUCCESS payment) and refunds; excludes PENDING/CANCELLED orders, REJECTED refunds, and Payouts
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var paidOrderId = Guid.NewGuid();
        var successPaymentId = Guid.NewGuid();
        var failedPaymentId = Guid.NewGuid();
        var pendingOrderId = Guid.NewGuid();

        context.Orders.AddRange(
            new Order
            {
                OrderId = paidOrderId, BuyerId = traineeId, CoachId = coachId,
                TotalAmount = 1500000m, OrderStatus = PaymentConstants.OrderStatusPaid,
                OrderType = PaymentConstants.OrderTypePackage,
                CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                OrderId = pendingOrderId, BuyerId = traineeId, CoachId = coachId,
                TotalAmount = 999000m, OrderStatus = PaymentConstants.OrderStatusPending,
                CreatedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc)
            });

        context.OrderDetails.Add(new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(), OrderId = paidOrderId, PackageTitle = "Fat Loss 30D"
        });

        context.Payments.AddRange(
            new Payment { PaymentId = failedPaymentId, OrderId = paidOrderId, Status = "FAILED", GatewayTransactionId = "GW-FAIL" },
            new Payment { PaymentId = successPaymentId, OrderId = paidOrderId, Status = "SUCCESS", GatewayTransactionId = "GW-OK" });

        var approvedRefundId = Guid.NewGuid();
        context.RefundRequests.AddRange(
            new RefundRequest
            {
                RefundRequestId = approvedRefundId, OrderId = paidOrderId, RequestedBy = traineeId,
                Status = "APPROVED", ApprovedAmount = 750000m, RequestedAmount = 1500000m,
                RefundTransactionRef = "REF-100",
                RefundedAt = new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc)
            },
            new RefundRequest
            {
                RefundRequestId = Guid.NewGuid(), OrderId = paidOrderId, RequestedBy = traineeId,
                Status = "REJECTED", RequestedAmount = 500000m,
                RequestedAt = new DateTime(2026, 5, 6, 10, 0, 0, DateTimeKind.Utc)
            });

        context.Payouts.Add(new Payout
        {
            PayoutId = Guid.NewGuid(), CoachId = traineeId, PayoutMonth = 5, PayoutYear = 2026,
            Status = "PROCESSED", NetPayoutAmount = 1000000m, ProcessedAt = new DateTime(2026, 5, 7, 10, 0, 0, DateTimeKind.Utc)
        });

        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 1, 10);
        var list = items.ToList();

        Assert.Equal(2, totalCount);
        Assert.Equal(2, list.Count);
        // Sorted descending by Date: Refund (May 5) first, Order (May 1) second
        Assert.Equal(approvedRefundId, list[0].TransactionId);
        Assert.Equal(750000m, list[0].Amount);
        Assert.Equal("REFUND", list[0].Type);
        Assert.Equal("Refund received: Fat Loss 30D", list[0].Description);
        Assert.Equal("REF-100", list[0].ReferenceId);

        Assert.Equal(successPaymentId, list[1].TransactionId);
        Assert.Equal(-1500000m, list[1].Amount);
        Assert.Equal("PACKAGE_PURCHASE", list[1].Type);
        Assert.Equal("Package purchased: Fat Loss 30D", list[1].Description);
        Assert.Equal("GW-OK", list[1].ReferenceId);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_ForCoach_ShouldReturnSoldPackagesActivationRefundAndPayout()
    {
        // Repo UTCID02 - Normal: Case-insensitive "coach" role aggregates sold packages (+), activation (-), refund (-), and payout (+)
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var soldOrderId = Guid.NewGuid();
        var activationOrderId = Guid.NewGuid();

        context.Orders.AddRange(
            new Order
            {
                OrderId = soldOrderId, BuyerId = traineeId, CoachId = coachId,
                TotalAmount = 3000000m, OrderStatus = PaymentConstants.OrderStatusCompleted,
                OrderType = PaymentConstants.OrderTypePackage,
                CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                OrderId = activationOrderId, BuyerId = coachId, CoachId = null,
                TotalAmount = 500000m, OrderStatus = PaymentConstants.OrderStatusPaid,
                OrderType = PaymentConstants.OrderTypeCoachActivation,
                CreatedAt = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc)
            });

        context.OrderDetails.Add(new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(), OrderId = soldOrderId, PackageTitle = "Pro Strength"
        });

        context.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(), OrderId = soldOrderId, RequestedBy = traineeId,
            Status = "REFUNDED", ApprovedAmount = 1000000m,
            RefundedAt = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc)
        });

        var payoutId = Guid.NewGuid();
        context.Payouts.Add(new Payout
        {
            PayoutId = payoutId, CoachId = coachId, PayoutMonth = 5, PayoutYear = 2026,
            Status = "PROCESSED", NetPayoutAmount = 2400000m, TransactionRef = "BANK-777",
            ProcessedAt = new DateTime(2026, 5, 4, 8, 0, 0, DateTimeKind.Utc)
        });
        context.PayoutItems.Add(new PayoutItem
        {
            PayoutItemId = Guid.NewGuid(), PayoutId = payoutId, OrderId = soldOrderId, NetAmount = 2400000m
        });

        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(coachId, "coach", null, null, null, 1, 10);
        var list = items.ToList();

        Assert.Equal(4, totalCount);
        Assert.Equal("PAYOUT", list[0].Type);
        Assert.Equal(2400000m, list[0].Amount);
        Assert.Equal("System payout: Pro Strength (Cycle 05/2026)", list[0].Description);
        Assert.Equal("BANK-777", list[0].ReferenceId);

        Assert.Equal("REFUND", list[1].Type);
        Assert.Equal(-1000000m, list[1].Amount);
        Assert.Equal("Refund deducted: Pro Strength", list[1].Description);

        Assert.Equal("COACH_ACTIVATION", list[2].Type);
        Assert.Equal(-500000m, list[2].Amount);
        Assert.Equal("Coach subscription activation / renewal", list[2].Description);

        Assert.Equal("PACKAGE_PURCHASE", list[3].Type);
        Assert.Equal(3000000m, list[3].Amount);
        Assert.Equal("Package sold: Pro Strength", list[3].Description);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenMatchedViaPackageCoachIdOrCoachBuysPackage_ShouldClassifySignsCorrectly()
    {
        // Repo UTCID03 - Normal: Order matched via Package.CoachId when Order.CoachId=null (+); coach buying package as BuyerId (-)
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var pkgId = Guid.NewGuid();

        context.TrainingPackages.Add(new TrainingPackage { PackageId = pkgId, CoachId = coachId, Title = "Mobility" });

        var soldViaPkgOrderId = Guid.NewGuid();
        var coachBoughtOrderId = Guid.NewGuid();

        context.Orders.AddRange(
            new Order
            {
                OrderId = soldViaPkgOrderId, BuyerId = traineeId, CoachId = null,
                TotalAmount = 1200000m, OrderStatus = PaymentConstants.OrderStatusActive,
                OrderType = PaymentConstants.OrderTypePackage,
                CreatedAt = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc)
            },
            new Order
            {
                OrderId = coachBoughtOrderId, BuyerId = coachId, CoachId = otherCoachId,
                TotalAmount = 800000m, OrderStatus = PaymentConstants.OrderStatusPaid,
                OrderType = PaymentConstants.OrderTypePackage,
                CreatedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc)
            });

        context.OrderDetails.AddRange(
            new OrderDetail { OrderDetailsId = Guid.NewGuid(), OrderId = soldViaPkgOrderId, PackageId = pkgId, PackageTitle = "Mobility" },
            new OrderDetail { OrderDetailsId = Guid.NewGuid(), OrderId = coachBoughtOrderId, PackageTitle = "Other Coach Plan" });

        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(coachId, "COACH", null, null, null, 1, 10);
        var list = items.ToList();

        Assert.Equal(2, totalCount);
        Assert.Equal(1200000m, list[0].Amount);
        Assert.Equal("Package sold: Mobility", list[0].Description);
        Assert.Equal(-800000m, list[1].Amount);
        Assert.Equal("Package purchased: Other Coach Plan", list[1].Description);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenFilteredByType_ShouldFilterCaseInsensitivelyOrReturnAllForAll()
    {
        // Repo UTCID04 - Normal: Filters by specific type ("refund") or returns all when type is "ALL"
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            OrderId = orderId, BuyerId = traineeId, CoachId = coachId,
            TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        context.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(), OrderId = orderId, RequestedBy = traineeId,
            Status = "PENDING", RequestedAmount = 500000m, RequestedAt = DateTime.UtcNow.AddDays(-1)
        });
        context.Payouts.Add(new Payout
        {
            PayoutId = Guid.NewGuid(), CoachId = coachId, PayoutMonth = 5, PayoutYear = 2026,
            Status = "PENDING", NetPayoutAmount = 900000m, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (refundOnly, refundCount) = await repo.GetUserTransactionsAsync(coachId, "COACH", null, null, "refund", 1, 10);
        var (allTypes, allCount) = await repo.GetUserTransactionsAsync(coachId, "COACH", null, null, "ALL", 1, 10);

        Assert.Equal(1, refundCount);
        Assert.Equal("REFUND", Assert.Single(refundOnly).Type);
        Assert.Equal(3, allCount);
        Assert.Equal(3, allTypes.Count());
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenFilteredByDateRange_ShouldFilterOrdersRefundsAndPayouts()
    {
        // Repo UTCID05 - Normal: Applies FromDate and ToDate across orders, refunds, and payouts
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var inRangeOrderId = Guid.NewGuid();
        var outRangeOrderId = Guid.NewGuid();

        context.Orders.AddRange(
            new Order { OrderId = inRangeOrderId, BuyerId = traineeId, CoachId = coachId, TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = new DateTime(2026, 5, 10, 5, 0, 0, DateTimeKind.Utc) },
            new Order { OrderId = outRangeOrderId, BuyerId = traineeId, CoachId = coachId, TotalAmount = 2000000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = new DateTime(2026, 4, 15, 5, 0, 0, DateTimeKind.Utc) });

        context.RefundRequests.AddRange(
            new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = inRangeOrderId, RequestedBy = traineeId, Status = "APPROVED", ApprovedAmount = 300000m, RefundedAt = new DateTime(2026, 5, 11, 5, 0, 0, DateTimeKind.Utc) },
            new RefundRequest { RefundRequestId = Guid.NewGuid(), OrderId = outRangeOrderId, RequestedBy = traineeId, Status = "APPROVED", ApprovedAmount = 400000m, RefundedAt = new DateTime(2026, 4, 16, 5, 0, 0, DateTimeKind.Utc) });

        context.Payouts.AddRange(
            new Payout { PayoutId = Guid.NewGuid(), CoachId = coachId, PayoutMonth = 5, PayoutYear = 2026, Status = "COMPLETED", NetPayoutAmount = 700000m, ProcessedAt = new DateTime(2026, 5, 12, 5, 0, 0, DateTimeKind.Utc) },
            new Payout { PayoutId = Guid.NewGuid(), CoachId = coachId, PayoutMonth = 4, PayoutYear = 2026, Status = "COMPLETED", NetPayoutAmount = 800000m, ProcessedAt = new DateTime(2026, 4, 20, 5, 0, 0, DateTimeKind.Utc) });

        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(
            coachId, "COACH", new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), null, 1, 10);

        Assert.Equal(3, totalCount);
        Assert.Equal(3, items.Count());
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenOptionalFieldsNull_ShouldUseDefaultFallbacks()
    {
        // Repo UTCID06 - Normal: No payments -> OrderId fallback; no PackageTitle -> "Training Package"; Payout null NetPayoutAmount & null TransactionRef
        using var context = CreateInMemoryDbContext();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var payoutId = Guid.NewGuid();

        context.Orders.Add(new Order
        {
            OrderId = orderId, BuyerId = traineeId, CoachId = coachId,
            TotalAmount = 1000000m, OrderStatus = PaymentConstants.OrderStatusPaid,
            CreatedAt = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc)
        });

        context.Payouts.Add(new Payout
        {
            PayoutId = payoutId, CoachId = coachId, PayoutMonth = 4, PayoutYear = 2026,
            Status = "PROCESSED", NetPayoutAmount = null, TransactionRef = null,
            CreatedAt = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc)
        });
        context.PayoutItems.AddRange(
            new PayoutItem { PayoutItemId = Guid.NewGuid(), PayoutId = payoutId, OrderId = orderId, NetAmount = 450000m },
            new PayoutItem { PayoutItemId = Guid.NewGuid(), PayoutId = payoutId, OrderId = orderId, NetAmount = 350000m });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(coachId, "COACH", null, null, null, 1, 10);
        var list = items.ToList();

        Assert.Equal(2, totalCount);
        Assert.Equal(payoutId, list[0].TransactionId);
        Assert.Equal(800000m, list[0].Amount);
        Assert.Equal("System payout (Cycle 04/2026)", list[0].Description);
        Assert.Equal($"PAYOUT-{payoutId}", list[0].ReferenceId);

        Assert.Equal(orderId, list[1].TransactionId);
        Assert.Equal("Package sold: Training Package", list[1].Description);
        Assert.Equal(orderId.ToString(), list[1].ReferenceId);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenPageIndexOrPageSizeBelow1_ShouldClampToDefaults()
    {
        // Repo UTCID07 - Boundary: pageIndex <= 0 clamped to 1; pageSize <= 0 clamped to 10; page 2 slice returns exact count
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        for (int i = 0; i < 12; i++)
        {
            context.Orders.Add(new Order
            {
                OrderId = Guid.NewGuid(), BuyerId = traineeId, CoachId = Guid.NewGuid(),
                TotalAmount = 100000m * (i + 1), OrderStatus = PaymentConstants.OrderStatusPaid,
                CreatedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i)
            });
        }
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (clampedItems, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 0, 0);
        var (page2Items, page2Total) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 2, 5);

        Assert.Equal(12, totalCount);
        Assert.Equal(10, clampedItems.Count());
        Assert.Equal(12, page2Total);
        Assert.Equal(5, page2Items.Count());
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenPageSizeExceeds100_ShouldClampToMax100()
    {
        // Repo UTCID08 - Boundary: pageSize > 100 clamped to maximum 100 items per page
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        for (int i = 0; i < 105; i++)
        {
            context.Orders.Add(new Order
            {
                OrderId = Guid.NewGuid(), BuyerId = traineeId, CoachId = Guid.NewGuid(),
                TotalAmount = 100000m, OrderStatus = PaymentConstants.OrderStatusPaid,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 1, 200);

        Assert.Equal(105, totalCount);
        Assert.Equal(100, items.Count());
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_VietnamTimezoneDateBoundaries_ShouldIncludeExactStartAndEndUtc()
    {
        // Repo UTCID09 - Boundary: For 2026-05-01 VN (UTC+7), startUtc=2026-04-30 17:00:00 UTC and endUtc=2026-05-01 16:59:59.9999999 UTC
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        var startUtc = new DateTime(2026, 4, 30, 17, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 5, 1, 17, 0, 0, DateTimeKind.Utc).AddTicks(-1);

        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 100000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = startUtc },
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 200000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = endUtc },
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 999000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = startUtc.AddSeconds(-1) },
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 888000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = endUtc.AddTicks(1) });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(
            traineeId, "TRAINEE", new DateTime(2026, 5, 1), new DateTime(2026, 5, 1), null, 1, 10);

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count());
        Assert.DoesNotContain(items, x => x.Amount == -999000m || x.Amount == -888000m);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenPageIndexBeyondLastPage_ShouldReturnEmptyItemsWithTotalCount()
    {
        // Repo UTCID10 - Boundary: pageIndex beyond available pages returns empty Items while preserving TotalCount
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        context.Orders.AddRange(
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 100000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow },
            new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 200000m, OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 5, 10);

        Assert.Equal(2, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenUserHasNoValidTransactions_ShouldReturnEmptyAndZeroCount()
    {
        // Repo UTCID11 - Boundary: No paid orders/refunds/payouts returns empty list and TotalCount = 0
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        context.Orders.Add(new Order { OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 100000m, OrderStatus = PaymentConstants.OrderStatusCancelled, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 1, 10);

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenAmountsAreNull_ShouldFallbackToZero()
    {
        // Repo UTCID12 - Boundary: Null TotalAmount on Order and null ApprovedAmount/RequestedAmount on Refund fall back to 0m
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        context.Orders.Add(new Order
        {
            OrderId = orderId, BuyerId = traineeId, TotalAmount = null,
            OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        context.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(), OrderId = orderId, RequestedBy = traineeId,
            Status = "PENDING", ApprovedAmount = null, RequestedAmount = null, RequestedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, null, 1, 10);

        Assert.Equal(2, totalCount);
        Assert.All(items, x => Assert.Equal(0m, x.Amount));
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenTypeFilterDoesNotMatchAnyRecord_ShouldReturnEmpty()
    {
        // Repo UTCID13 - Boundary: Unmatched type filter returns empty result and TotalCount = 0
        using var context = CreateInMemoryDbContext();
        var traineeId = Guid.NewGuid();
        context.Orders.Add(new Order
        {
            OrderId = Guid.NewGuid(), BuyerId = traineeId, TotalAmount = 500000m,
            OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(traineeId, "TRAINEE", null, null, "NON_EXISTENT_TYPE", 1, 10);

        Assert.Equal(0, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenUserRoleIsNullOrEmpty_ShouldTreatAsNonCoachSafely()
    {
        // Repo UTCID14 - Abnormal: Null userRole does not throw and excludes coach-only Payout records
        using var context = CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        context.Orders.Add(new Order
        {
            OrderId = Guid.NewGuid(), BuyerId = userId, TotalAmount = 500000m,
            OrderStatus = PaymentConstants.OrderStatusPaid, CreatedAt = DateTime.UtcNow
        });
        context.Payouts.Add(new Payout
        {
            PayoutId = Guid.NewGuid(), CoachId = userId, PayoutMonth = 5, PayoutYear = 2026,
            Status = "PROCESSED", NetPayoutAmount = 1000000m, ProcessedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new TransactionRepository(context);
        var (items, totalCount) = await repo.GetUserTransactionsAsync(userId, null!, null, null, null, 1, 10);

        Assert.Equal(1, totalCount);
        Assert.DoesNotContain(items, x => x.Type == "PAYOUT");
    }

    [Fact]
    public async Task Repository_GetUserTransactionsAsync_WhenCancelled_ShouldThrowOperationCanceledException()
    {
        // Repo UTCID15 - Abnormal: Pre-cancelled CancellationToken throws OperationCanceledException
        using var context = CreateInMemoryDbContext();
        var repo = new TransactionRepository(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repo.GetUserTransactionsAsync(Guid.NewGuid(), "TRAINEE", null, null, null, 1, 10, cts.Token));
    }

    #endregion
}

