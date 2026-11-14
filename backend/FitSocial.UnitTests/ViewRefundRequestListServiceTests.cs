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

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - GetPagedRefundRequestsAsync_NoFilter_ReturnsAllRequests
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: empty/null
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_NoFilter_ReturnsAllRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
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
    }

    /// <summary>
    /// UTCID02 - GetPagedRefundRequestsAsync_NoMatchingRecords_ReturnsEmptyList
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: empty/null
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_NoMatchingRecords_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto();
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync(null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RefundRequest>(), 0));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }

    /// <summary>
    /// UTCID03 - GetPagedRefundRequestsAsync_FilterByStatusPending_ReturnsPendingOnly
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByStatusPending_ReturnsPendingOnly()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "PENDING" };
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

    /// <summary>
    /// UTCID04 - GetPagedRefundRequestsAsync_FilterByStatusApproved_ReturnsApprovedOnly
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "APPROVED"
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByStatusApproved_ReturnsApprovedOnly()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "APPROVED" };
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

    /// <summary>
    /// UTCID05 - GetPagedRefundRequestsAsync_FilterBySearchTerm_ReturnsMatchingRequests
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: empty/null
    /// - SearchTerm: "John"
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterBySearchTerm_ReturnsMatchingRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { SearchTerm = "John" };
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
        Assert.Equal(1, result.Data.TotalCount);
    }

    /// <summary>
    /// UTCID06 - GetPagedRefundRequestsAsync_CombinedFilters_ReturnsFilteredRequests
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - SearchTerm: "John"
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_CombinedFilters_ReturnsFilteredRequests()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto
        {
            Status = "PENDING",
            SearchTerm = "John"
        };
        var list = new List<RefundRequest> { CreateRefundRequest(status: "PENDING", traineeName: "John Doe") };
        _refundRepoMock.Setup(r => r.GetPagedRefundRequestsAsync("PENDING", null, null, "John", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        // Act
        var result = await _service.GetPagedRefundRequestsAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal("PENDING", result.Data.Items[0].Status);
        Assert.Equal("John Doe", result.Data.Items[0].RequestedByName);
        Assert.Equal(1, result.Data.TotalCount);
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID07 - GetPagedRefundRequestsAsync_FilterByStatusRejected_ReturnsRejectedOnly
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "REJECTED"
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return PagedResultDto<RefundRequestDto> with Success=true
    /// Type: B
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_FilterByStatusRejected_ReturnsRejectedOnly()
    {
        // Arrange
        var query = new GetRefundRequestsQueryDto { Status = "REJECTED" };
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

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID08 - GetPagedRefundRequestsAsync_DatabaseConnectionFailure_ThrowsException
    /// Condition:
    /// - Precondition: Database connection failure
    /// - Status: empty/null
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Exception ("Database connection failure")
    /// Type: A
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetPagedRefundRequestsAsync_DatabaseConnectionFailure_ThrowsException()
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

    /// <summary>
    /// UTCID09 - GetPagedRefundRequestsAsync_CancelledToken_ThrowsOperationCanceledException
    /// Condition:
    /// - Precondition: Cancelled token
    /// - Status: empty/null
    /// - SearchTerm: empty/null
    /// - DatabaseResults: Not found
    /// Expected: OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    #endregion
}
