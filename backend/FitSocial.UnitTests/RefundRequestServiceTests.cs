using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Refunds;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class RefundRequestServiceTests
{
    private readonly Mock<IRefundRequestRepository> _refundRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RefundRequestService _service;

    public RefundRequestServiceTests()
    {
        _refundRepoMock = new Mock<IRefundRequestRepository>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new RefundRequestService(_refundRepoMock.Object, _orderRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_ValidOrder_ShouldCreateRefundRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            BuyerId = userId,
            OrderStatus = "PAID",
            TotalAmount = 500000,
            Payments = new List<Payment>
            {
                new() { PaymentId = Guid.NewGuid(), Status = "PAID" }
            }
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Coach did not attend sessions as agreed.",
            EvidenceUrls = new List<string> { "https://example.com/chat_screenshot.jpg" }
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _refundRepoMock.Setup(r => r.HasPendingRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _refundRepoMock.Setup(r => r.AddAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateRefundRequestAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(500000, result.Data.RequestedAmount);
        Assert.Equal("PENDING", result.Data.Status);
        Assert.Single(result.Data.EvidenceUrls);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_NotOrderBuyer_ShouldThrowValidationException()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            BuyerId = buyerId,
            OrderStatus = "PAID",
            TotalAmount = 500000
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Unauthorized request attempt."
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateRefundRequestAsync(otherUserId, dto));
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_Approve_ShouldSetStatusApprovedAndRefundOrder()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order { OrderId = Guid.NewGuid(), OrderStatus = "PAID" };
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        var dto = new ProcessRefundRequestDto
        {
            Status = "APPROVED",
            ApprovedAmount = 500000,
            RefundTransactionRef = "REF-20261002-001",
            StaffNote = "Refund approved after checking logs."
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ProcessRefundRequestAsync(refundId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("APPROVED", result.Data.Status);
        Assert.Equal(500000, result.Data.ApprovedAmount);
        Assert.Equal("REF-20261002-001", result.Data.RefundTransactionRef);
        Assert.Equal("REFUNDED", order.OrderStatus);
    }

    [Fact]
    public async Task GetRefundRequestByIdAsync_OwnerUser_ShouldReturnDetails()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = Guid.NewGuid(),
            RequestedBy = userId,
            RequestedAmount = 250000,
            Status = "PENDING"
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);

        // Act
        var result = await _service.GetRefundRequestByIdAsync(refundId, userId, isAdminOrStaff: false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(250000, result.Data.RequestedAmount);
    }

    [Fact]
    public async Task GetRefundRequestByIdAsync_UnauthorizedUser_ShouldThrowForbiddenException()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var strangerUserId = Guid.NewGuid();
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = Guid.NewGuid(),
            RequestedBy = ownerUserId,
            RequestedAmount = 250000,
            Status = "PENDING"
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetRefundRequestByIdAsync(refundId, strangerUserId, isAdminOrStaff: false));
    }
}
