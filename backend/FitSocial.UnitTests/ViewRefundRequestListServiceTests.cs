using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Refunds;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewRefundRequestListServiceTests
{
    private readonly Mock<IRefundRequestRepository> _refundRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<ITrainingPlanRepository> _trainingPlanRepoMock;
    private readonly Mock<ITermsAndPolicyRepository> _termsRepoMock;
    private readonly RefundRequestService _service;

    public ViewRefundRequestListServiceTests()
    {
        _refundRepoMock = new Mock<IRefundRequestRepository>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _auditLogRepoMock = new Mock<IAuditLogRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _trainingPlanRepoMock = new Mock<ITrainingPlanRepository>();
        _termsRepoMock = new Mock<ITermsAndPolicyRepository>();

        _service = new RefundRequestService(
            _refundRepoMock.Object,
            _orderRepoMock.Object,
            _unitOfWorkMock.Object,
            _auditLogRepoMock.Object,
            _notificationRepoMock.Object,
            _trainingPlanRepoMock.Object,
            _termsRepoMock.Object);
    }

    private static RefundRequest CreateRefundRequest(
        Guid? id = null,
        string status = "PENDING",
        string? reason = "Dissatisfied with schedule",
        decimal amount = 500000m,
        string? traineeName = "Nguyen Van A",
        string? traineeEmail = "trainee@example.com")
    {
        var requestId = id ?? Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        return new RefundRequest
        {
            RefundRequestId = requestId,
            OrderId = orderId,
            RequestedBy = userId,
            Status = status,
            Reason = reason,
            RequestedAmount = amount,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            RequestedByNavigation = new User
            {
                UserId = userId,
                FullName = traineeName,
                Email = traineeEmail
            },
            Order = new Order
            {
                OrderId = orderId,
                TotalAmount = amount,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        OrderDetailsId = Guid.NewGuid(),
                        OrderId = orderId,
                        PackageTitle = "Personal Training 1-on-1",
                        PackagePrice = amount,
                        PackageDurationDays = 30
                    }
                }
            }
        };
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_DefaultQuery_ReturnsPagedRefundRequestsSuccessfully()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest>
        {
            CreateRefundRequest(status: "PENDING"),
            CreateRefundRequest(status: "APPROVED")
        };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 2));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Refund requests retrieved successfully.", result.Message);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(10, result.Data.PageSize);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByPendingStatus_ReturnsFilteredRefundRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "PENDING", PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest> { CreateRefundRequest(status: "PENDING") };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync("PENDING", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("PENDING", result.Data.Items[0].Status);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByApprovedStatus_ReturnsFilteredRefundRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "APPROVED", PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest> { CreateRefundRequest(status: "APPROVED") };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync("APPROVED", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("APPROVED", result.Data.Items[0].Status);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByRejectedStatus_ReturnsFilteredRefundRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "REJECTED", PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest> { CreateRefundRequest(status: "REJECTED") };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync("REJECTED", null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("REJECTED", result.Data.Items[0].Status);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterBySearchTerm_ReturnsMatchingRefundRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { SearchTerm = "John", PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest> { CreateRefundRequest(traineeName: "John Doe") };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, "John", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("John Doe", result.Data.Items[0].RequestedByName);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByDateRange_ReturnsRefundRequestsWithinRange()
    {
        // Arrange
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var query = new GetRefundRequestsQueryDto { FromDate = from, ToDate = to, PageNumber = 1, PageSize = 10 };
        var list = new List<RefundRequest> { CreateRefundRequest() };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, from, to, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal(1, result.Data.TotalCount);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_WithItemHavingOrderDetails_EnrichesPolicyDetailsSuccessfully()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
        var refund = CreateRefundRequest();
        var list = new List<RefundRequest> { refund };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("Personal Training 1-on-1", result.Data.Items[0].PackageTitle);
        Assert.Equal(30, result.Data.Items[0].PackageDurationDays);
        Assert.Equal(500000m, result.Data.Items[0].PackageSnapshotPrice);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_WhenPolicyEnrichmentFails_StillReturnsDtoWithoutFailing()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
        var orderId = Guid.NewGuid();
        var refund = new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            OrderId = orderId,
            Status = "PENDING",
            Order = null! // Forces EnrichDtoWithPolicyAsync to call _orderRepository.GetOrderByIdWithDetailsAsync
        };
        _orderRepoMock.Setup(o => o.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Order db connection error"));
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RefundRequest> { refund }, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal(refund.RefundRequestId, result.Data.Items[0].RefundRequestId);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_NullQuery_InitializesDefaultQueryAndReturnsResults()
    {
        // Arrange
        GetRefundRequestsQueryDto? query = null;
        var list = new List<RefundRequest> { CreateRefundRequest() };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query!);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(1, result.Data.PageNumber);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Single(result.Data.Items);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_EmptyResult_ReturnsEmptyPagedList()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { PageNumber = 1, PageSize = 10 };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RefundRequest>(), 0));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Equal(0, result.Data.TotalPages);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_CustomPagination_ReturnsCorrectPageAndPageSize()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { PageNumber = 2, PageSize = 5 };
        var list = new List<RefundRequest>
        {
            CreateRefundRequest(),
            CreateRefundRequest(),
            CreateRefundRequest(),
            CreateRefundRequest(),
            CreateRefundRequest()
        };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 15));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data.PageNumber);
        Assert.Equal(5, result.Data.PageSize);
        Assert.Equal(15, result.Data.TotalCount);
        Assert.Equal(5, result.Data.Items.Count);
        Assert.Equal(3, result.Data.TotalPages);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_RepositoryThrowsException_ThrowsException()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.GetPagedRefundRequestsAsync(query));
        Assert.Equal("Database connection failure", ex.Message);
    }

    [Fact]
    public async Task GetPagedRefundRequestsAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.GetPagedRefundRequestsAsync(query, cts.Token));
    }
}
