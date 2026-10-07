using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Refunds;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class RefundRequestServiceTests
{
    private readonly Mock<IRefundRequestRepository> _refundRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<ITrainingPlanRepository> _trainingPlanRepoMock;
    private readonly Mock<ITermsAndPolicyRepository> _termsRepoMock;
    private readonly RefundRequestService _service;

    public RefundRequestServiceTests()
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

    [Fact]
    public async Task CreateRefundRequestAsync_ValidOrderWithin7Days_ShouldCreateRefundRequest()
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
            CreatedAt = DateTime.UtcNow.AddDays(-2), // 2 days ago (within 7 days)
            Payments = new List<Payment>
            {
                new() { PaymentId = Guid.NewGuid(), Status = "PAID", ProcessedAt = DateTime.UtcNow.AddDays(-2) }
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
        _refundRepoMock.Setup(r => r.HasApprovedRefundAsync(orderId, It.IsAny<CancellationToken>()))
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
    public async Task CreateRefundRequestAsync_After7Days_ShouldThrowBusinessException()
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
            CreatedAt = DateTime.UtcNow.AddDays(-8), // 8 days ago (expired!)
            Payments = new List<Payment>
            {
                new() { PaymentId = Guid.NewGuid(), Status = "PAID", ProcessedAt = DateTime.UtcNow.AddDays(-8) }
            }
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Too late request"
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.CreateRefundRequestAsync(userId, dto));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_WhenCoachPayoutAlreadyProcessed_ShouldThrowBusinessException()
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
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            PayoutItems = new List<PayoutItem>
            {
                new()
                {
                    Payout = new Payout { Status = PayoutConstants.StatusProcessed }
                }
            }
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Refund requested but coach already paid out"
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.CreateRefundRequestAsync(userId, dto));
        Assert.Contains("payout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_UsesFinancialSnapshotNotPackageCurrentPrice()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            BuyerId = userId,
            OrderStatus = "PAID",
            TotalAmount = 450000, // Purchased at discounted 450,000 VND
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    PackagePrice = 450000,
                    Package = new TrainingPackage { Price = 1000000 } // Price now increased to 1,000,000 VND!
                }
            }
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Refund requested"
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _refundRepoMock.Setup(r => r.HasPendingRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _refundRepoMock.Setup(r => r.HasApprovedRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _refundRepoMock.Setup(r => r.AddAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateRefundRequestAsync(userId, dto);

        // Assert - MUST snapshot 450,000, NOT 1,000,000
        Assert.NotNull(result);
        Assert.Equal(450000, result.Data.RequestedAmount);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_DuplicatePendingRequest_ShouldThrowValidationException()
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
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var dto = new CreateRefundRequestDto { OrderId = orderId, Reason = "Duplicate" };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _refundRepoMock.Setup(r => r.HasPendingRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already has pending refund

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateRefundRequestAsync(userId, dto));
    }

    [Fact]
    public async Task ApproveRefundRequestAsync_NormalApproval_ShouldSetStatusApprovedAndRefundOrder()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderStatus = "PAID",
            TotalAmount = 500000
        };
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        var dto = new ApproveRefundRequestDto
        {
            ApprovedAmount = 500000,
            RefundTransactionRef = "REF-20261002-001",
            StaffNote = "Refund approved after checking logs."
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ApproveRefundRequestAsync(refundId, staffId, dto);

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
    public async Task ApproveRefundRequestAsync_ConcurrentDoubleApproval_ShouldThrowBusinessException()
    {
        // Arrange: Simulates Staff B committing after Staff A already committed
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order { OrderId = Guid.NewGuid(), OrderStatus = "PAID", TotalAmount = 500000 };
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        var dto = new ApproveRefundRequestDto { ApprovedAmount = 500000 };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // DbUpdateConcurrencyException simulates EF Core concurrency token mismatch
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.ApproveRefundRequestAsync(refundId, staffId, dto));
        Assert.Contains("another staff member", ex.Message, StringComparison.OrdinalIgnoreCase);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveRefundRequestAsync_WhenCoachPayoutAlreadyProcessed_ShouldThrowBusinessException()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderStatus = "PAID",
            TotalAmount = 500000,
            PayoutItems = new List<PayoutItem>
            {
                new() { Payout = new Payout { Status = PayoutConstants.StatusProcessed } }
            }
        };
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ApproveRefundRequestAsync(refundId, staffId, new ApproveRefundRequestDto()));
        Assert.Contains("payout to the coach has already been processed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectRefundRequestAsync_ValidNote_ShouldSetStatusRejectedAndSaveNote()
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

        var dto = new RejectRefundRequestDto { StaffNote = "Customer already completed 10 workouts." };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RejectRefundRequestAsync(refundId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REJECTED", result.Data.Status);
        Assert.Equal("Customer already completed 10 workouts.", result.Data.StaffNote);
        Assert.Equal("PAID", order.OrderStatus); // Order remains unchanged
    }

    [Fact]
    public async Task RejectRefundRequestAsync_EmptyNote_ShouldThrowValidationException()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var dto = new RejectRefundRequestDto { StaffNote = "" };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _service.RejectRefundRequestAsync(refundId, staffId, dto));
    }

    [Fact]
    public async Task ApproveRefundRequestAsync_AlreadyProcessedRequest_ShouldThrowValidationException()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            Status = "APPROVED" // Already processed
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ApproveRefundRequestAsync(refundId, staffId, new ApproveRefundRequestDto()));
        Assert.Contains("already been processed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRefundRequestAsync_WithBuyerAndCoach_ShouldNotifyBothAndRecordAuditLog()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = orderId,
            BuyerId = buyerId,
            CoachId = coachId,
            OrderStatus = "PAID",
            TotalAmount = 750000,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Payments = new List<Payment>
            {
                new() { PaymentId = Guid.NewGuid(), Status = "PAID", ProcessedAt = DateTime.UtcNow.AddDays(-1) }
            },
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderDetailsId = Guid.NewGuid(),
                    OrderId = orderId,
                    PackageTitle = "Strength 101",
                    Package = new TrainingPackage { Title = "Strength 101" }
                }
            }
        };

        var dto = new CreateRefundRequestDto
        {
            OrderId = orderId,
            Reason = "Could not reach agreement on workout schedule.",
            EvidenceUrls = new List<string>()
        };

        _orderRepoMock.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _refundRepoMock.Setup(r => r.HasPendingRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _refundRepoMock.Setup(r => r.HasApprovedRefundAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateRefundRequestAsync(buyerId, dto);

        // Assert
        Assert.True(result.Success);
        // Verify notification for buyer
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == buyerId && notif.Type == "REFUND_SUBMITTED" && notif.Description != null && notif.Description.Contains("Strength 101")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify notification for seller/coach
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == coachId && notif.Type == "REFUND_REQUESTED" && notif.Description != null && notif.Description.Contains("Strength 101")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify audit log
        _auditLogRepoMock.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log => log.ActorAccountId == buyerId && log.Action == "CREATE_REFUND_REQUEST" && log.EntityType == "RefundRequest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveRefundRequestAsync_WithBuyerAndCoach_ShouldNotifyBothAndRecordAuditLog()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = buyerId,
            CoachId = coachId,
            OrderStatus = "PAID",
            TotalAmount = 500000,
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderDetailsId = Guid.NewGuid(),
                    PackageTitle = "Yoga Basics",
                    Package = new TrainingPackage { Title = "Yoga Basics" }
                }
            }
        };

        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            RequestedBy = buyerId,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        var dto = new ApproveRefundRequestDto
        {
            ApprovedAmount = 500000,
            RefundTransactionRef = "TXN-APPROVE-123",
            StaffNote = "Legitimate request, refunded."
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ApproveRefundRequestAsync(refundId, staffId, dto);

        // Assert
        Assert.True(result.Success);
        // Verify buyer notification
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == buyerId && notif.Type == "REFUND_APPROVED" && notif.Description != null && notif.Description.Contains("Yoga Basics")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify coach notification
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == coachId && notif.Type == "REFUND_APPROVED" && notif.Description != null && notif.Description.Contains("Yoga Basics")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify audit log
        _auditLogRepoMock.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log => log.ActorAccountId == staffId && log.Action == "APPROVE_REFUND" && log.EntityType == "RefundRequest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectRefundRequestAsync_WithBuyerAndCoach_ShouldNotifyBothAndRecordAuditLog()
    {
        // Arrange
        var refundId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = buyerId,
            CoachId = coachId,
            OrderStatus = "PAID",
            TotalAmount = 500000,
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderDetailsId = Guid.NewGuid(),
                    PackageTitle = "Pilates Pro",
                    Package = new TrainingPackage { Title = "Pilates Pro" }
                }
            }
        };

        var refundRequest = new RefundRequest
        {
            RefundRequestId = refundId,
            OrderId = order.OrderId,
            Order = order,
            RequestedBy = buyerId,
            Status = "PENDING",
            RequestedAmount = 500000
        };

        var dto = new RejectRefundRequestDto
        {
            StaffNote = "Services have already been rendered in full."
        };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(refundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundRequest);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.RejectRefundRequestAsync(refundId, staffId, dto);

        // Assert
        Assert.True(result.Success);
        // Verify buyer notification
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == buyerId && notif.Type == "REFUND_REJECTED" && notif.Description != null && notif.Description.Contains("Pilates Pro") && notif.Description.Contains("Services have already been rendered in full.")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify coach notification
        _notificationRepoMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif => notif.UserId == coachId && notif.Type == "REFUND_REJECTED" && notif.Description != null && notif.Description.Contains("Pilates Pro")),
            It.IsAny<CancellationToken>()), Times.Once);
        // Verify audit log
        _auditLogRepoMock.Verify(a => a.AddAsync(
            It.Is<AuditLog>(log => log.ActorAccountId == staffId && log.Action == "REJECT_REFUND" && log.EntityType == "RefundRequest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

