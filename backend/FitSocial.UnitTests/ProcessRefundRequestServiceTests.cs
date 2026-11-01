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
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ProcessRefundRequestServiceTests
{
    private readonly Mock<IRefundRequestRepository> _refundRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly Mock<ITrainingPlanRepository> _trainingPlanRepoMock;
    private readonly Mock<ITermsAndPolicyRepository> _termsRepoMock;
    private readonly RefundRequestService _service;

    public ProcessRefundRequestServiceTests()
    {
        _refundRepoMock = new Mock<IRefundRequestRepository>();
        _orderRepoMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _auditLogRepoMock = new Mock<IAuditLogRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();
        _trainingPlanRepoMock = new Mock<ITrainingPlanRepository>();
        _termsRepoMock = new Mock<ITermsAndPolicyRepository>();

        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

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
        Guid? orderId = null,
        Guid? userId = null,
        string status = "PENDING",
        decimal amount = 500000m)
    {
        var reqId = id ?? Guid.NewGuid();
        var ordId = orderId ?? Guid.NewGuid();
        var usrId = userId ?? Guid.NewGuid();

        var order = new Order
        {
            OrderId = ordId,
            BuyerId = usrId,
            TotalAmount = amount,
            OrderStatus = PaymentConstants.OrderStatusPaid,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            PayoutItems = new List<PayoutItem>(),
            Payments = new List<Payment>
            {
                new()
                {
                    PaymentId = Guid.NewGuid(),
                    OrderId = ordId,
                    Amount = amount,
                    Status = PaymentConstants.PaymentStatusSuccess,
                    ProcessedAt = DateTime.UtcNow.AddDays(-2)
                }
            },
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderDetailsId = Guid.NewGuid(),
                    OrderId = ordId,
                    PackageTitle = "Personal Training 1-on-1",
                    PackagePrice = amount,
                    PackageDurationDays = 30
                }
            }
        };

        return new RefundRequest
        {
            RefundRequestId = reqId,
            OrderId = ordId,
            RequestedBy = usrId,
            RequestedAmount = amount,
            Status = status,
            Reason = "Schedule conflict",
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Order = order,
            RequestedByNavigation = new User
            {
                UserId = usrId,
                FullName = "Nguyen Van A",
                Email = "trainee@example.com"
            }
        };
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_StatusApproved_CallsApproveAndReturnsSuccess()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "APPROVED",
            ApprovedAmount = 500000m,
            StaffNote = "Refund approved by staff",
            RefundTransactionRef = "TXREF123456"
        };

        // Act
        var result = await _service.ProcessRefundRequestAsync(requestId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("APPROVED", result.Data.Status);
        Assert.Equal(500000m, result.Data.ApprovedAmount);
        Assert.Equal(staffId, item.ReviewedBy);
        Assert.Equal(PaymentConstants.OrderStatusRefunded, item.Order.OrderStatus);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_StatusApprovedMinimalData_UsesDefaultAmountAndSucceeds()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "APPROVED",
            ApprovedAmount = null,
            StaffNote = null
        };

        // Act
        var result = await _service.ProcessRefundRequestAsync(requestId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("APPROVED", result.Data.Status);
        Assert.Equal(500000m, result.Data.ApprovedAmount);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_StatusRejectedWithNote_CallsRejectAndReturnsSuccess()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "REJECTED",
            StaffNote = "Invalid refund evidence provided"
        };

        // Act
        var result = await _service.ProcessRefundRequestAsync(requestId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("REJECTED", result.Data.Status);
        Assert.Equal("Invalid refund evidence provided", item.StaffNote);
        Assert.Equal(staffId, item.ReviewedBy);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_StatusRejectedWithoutNote_UsesDefaultNoteAndSucceeds()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "REJECTED",
            StaffNote = null // Triggers fallback to "Rejected by staff."
        };

        // Act
        var result = await _service.ProcessRefundRequestAsync(requestId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("REJECTED", result.Data.Status);
        Assert.Equal("Rejected by staff.", item.StaffNote);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_StatusCaseInsensitiveAndWhitespace_ProcessesSuccessfully()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "  approved  ",
            StaffNote = "Valid request"
        };

        // Act
        var result = await _service.ProcessRefundRequestAsync(requestId, staffId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("APPROVED", result.Data.Status);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_ApprovedAmountExceedsOrderTotal_ThrowsValidationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId, amount: 500000m);

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto
        {
            Status = "APPROVED",
            ApprovedAmount = 1000000m // Greater than 500,000 VND
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto));
        Assert.Contains("cannot exceed the original paid amount", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_NullDto_ThrowsValidationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, null!));
        Assert.Equal("Process payload cannot be null.", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_InvalidStatusValue_ThrowsValidationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto
        {
            Status = "CANCELLED"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto));
        Assert.Equal("Status must be either 'APPROVED' or 'REJECTED'.", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_EmptyRefundRequestId_ThrowsValidationException()
    {
        // Arrange
        var staffId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(Guid.Empty, staffId, dto));
        Assert.Equal("Refund Request ID is required.", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_EmptyStaffUserId_ThrowsValidationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(requestId, Guid.Empty, dto));
        Assert.Equal("Staff user ID is required.", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_RefundRequestNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto));
        Assert.Equal("Refund request not found.", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_AlreadyProcessedRequest_ThrowsValidationException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var item = CreateRefundRequest(id: requestId, status: "APPROVED");

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto));
        Assert.Contains("already been processed", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_RepositoryThrowsException_ThrowsException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto));
        Assert.Equal("Database connection failure", ex.Message);
    }

    [Fact]
    public async Task ProcessRefundRequestAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var dto = new ProcessRefundRequestDto { Status = "APPROVED" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _refundRepoMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ProcessRefundRequestAsync(requestId, staffId, dto, cts.Token));
    }
}

