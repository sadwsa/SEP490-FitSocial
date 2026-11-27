using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for View Purchased Training Packages Details feature:
/// OrderService.GetOrderByIdAsync(Guid orderId, Guid currentUserId, CancellationToken cancellationToken, bool isAdmin)
/// Covers:
/// - Normal test cases (UTCID01 - UTCID10, UTCID21): Trainee/Coach/Admin access, multiple details, fallbacks, statuses
/// - Boundary test cases (UTCID11 - UTCID16, UTCID22): 0 details, zero/high amount, same buyer&coach, Guid.Empty, package coach fallback
/// - Abnormal test cases (UTCID17 - UTCID20): Order not found, Forbidden access, DB error, CancellationToken
/// </summary>
public class ViewPurchasedTrainingPackageDetailsServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ITrainingPackageRepository> _mockPackageRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly OrderService _service;

    public ViewPurchasedTrainingPackageDetailsServiceTests()
    {
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockPackageRepo = new Mock<ITrainingPackageRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new OrderService(
            _mockOrderRepo.Object,
            _mockPackageRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object);
    }

    #region Helper Methods

    private static Order CreateSampleOrder(
        Guid orderId,
        Guid buyerId,
        Guid coachId,
        string orderStatus = "PAID",
        decimal totalAmount = 1500000m,
        string buyerFullName = "Nguyen Van Trainee",
        string buyerEmail = "trainee@fitsocial.com",
        string coachFullName = "Coach Tran Van",
        List<OrderDetail>? details = null)
    {
        var buyer = new User
        {
            UserId = buyerId,
            FullName = buyerFullName,
            Email = buyerEmail
        };

        var coachUser = new User
        {
            UserId = coachId,
            FullName = coachFullName,
            Email = "coach@fitsocial.com"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = coachUser
        };

        var defaultDetails = details ?? new List<OrderDetail>
        {
            new OrderDetail
            {
                OrderDetailsId = Guid.NewGuid(),
                OrderId = orderId,
                PackageId = Guid.NewGuid(),
                PackageTitle = "30-Day Fat Loss & Muscle Building",
                PackagePrice = totalAmount,
                PackageDurationDays = 30,
                PackageType = "BOTH",
                CoachName = coachFullName,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Package = new TrainingPackage
                {
                    Title = "30-Day Fat Loss & Muscle Building",
                    DurationDays = 30,
                    Price = totalAmount,
                    PackageType = "BOTH"
                }
            }
        };

        return new Order
        {
            OrderId = orderId,
            BuyerId = buyerId,
            Buyer = buyer,
            CoachId = coachId,
            Coach = coachProfile,
            TotalAmount = totalAmount,
            OrderStatus = orderStatus,
            OrderType = "TRAINING_PACKAGE",
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            OrderDetails = defaultDetails
        };
    }

    #endregion

    #region Normal Test Cases (UTCID01 - UTCID10)

    [Fact]
    public async Task UTCID01_GetOrderByIdAsync_TraineeViewsOwnPurchasedPackageOrder_ReturnsOrderDtoSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(orderId, result.Data.OrderId);
        Assert.Equal(traineeId, result.Data.BuyerId);
        Assert.Equal("Nguyen Van Trainee", result.Data.BuyerName);
        Assert.Equal("trainee@fitsocial.com", result.Data.BuyerEmail);
        Assert.Equal(coachId, result.Data.CoachId);
        Assert.Equal("Coach Tran Van", result.Data.CoachName);
        Assert.Single(result.Data.Details);
        Assert.Equal("30-Day Fat Loss & Muscle Building", result.Data.Details[0].PackageTitle);
        Assert.Equal(1500000m, result.Data.Details[0].PackagePrice);
    }

    [Fact]
    public async Task UTCID02_GetOrderByIdAsync_CoachViewsTraineePurchasedPackageOrder_ReturnsOrderDtoSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act - Coach requests the order
        var result = await _service.GetOrderByIdAsync(orderId, coachId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(orderId, result.Data.OrderId);
        Assert.Equal(coachId, result.Data.CoachId);
    }

    [Fact]
    public async Task UTCID03_GetOrderByIdAsync_OrderWithMultiplePackageDetails_MapsAllDetailsCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var details = new List<OrderDetail>
        {
            new OrderDetail
            {
                OrderDetailsId = Guid.NewGuid(),
                OrderId = orderId,
                PackageId = Guid.NewGuid(),
                PackageTitle = "Package Part 1: Strength Training",
                PackagePrice = 1000000m,
                PackageDurationDays = 30,
                PackageType = "WORKOUT",
                CoachName = "Coach Tran Van",
                CreatedAt = DateTime.UtcNow
            },
            new OrderDetail
            {
                OrderDetailsId = Guid.NewGuid(),
                OrderId = orderId,
                PackageId = Guid.NewGuid(),
                PackageTitle = "Package Part 2: Custom Nutrition Plan",
                PackagePrice = 500000m,
                PackageDurationDays = 30,
                PackageType = "MEAL",
                CoachName = "Coach Tran Van",
                CreatedAt = DateTime.UtcNow
            }
        };

        var order = CreateSampleOrder(orderId, traineeId, coachId, totalAmount: 1500000m, details: details);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal(2, result.Data.Details.Count);
        Assert.Equal("Package Part 1: Strength Training", result.Data.Details[0].PackageTitle);
        Assert.Equal("Package Part 2: Custom Nutrition Plan", result.Data.Details[1].PackageTitle);
    }

    [Fact]
    public async Task UTCID04_GetOrderByIdAsync_BuyerFullNameIsNull_FallsBackToBuyerEmail()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);
        order.Buyer!.FullName = null; // FullName is null

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("trainee@fitsocial.com", result.Data.BuyerName);
    }

    [Fact]
    public async Task UTCID05_GetOrderByIdAsync_CoachNavigationIsNull_FallsBackToOrderDetailCoachName()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);
        order.Coach = null; // Coach profile navigation is null
        order.OrderDetails.First().CoachName = "Coach Fallback From Detail";

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("Coach Fallback From Detail", result.Data.CoachName);
    }

    [Fact]
    public async Task UTCID06_GetOrderByIdAsync_OrderDetailPackageTitleIsNull_FallsBackToPackageTitle()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);
        var detail = order.OrderDetails.First();
        detail.PackageTitle = null; // Null snapshot title
        detail.Package = new TrainingPackage { Title = "Live Masterclass Training" };

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("Live Masterclass Training", result.Data.Details[0].PackageTitle);
    }

    [Fact]
    public async Task UTCID07_GetOrderByIdAsync_OrderDetailDurationDaysIsNull_FallsBackToPackageDurationDays()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);
        var detail = order.OrderDetails.First();
        detail.PackageDurationDays = null; // Null duration
        detail.Package = new TrainingPackage { Title = "Package Title", DurationDays = 45 };

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal(45, result.Data.Details[0].PackageDurationDays);
    }

    [Fact]
    public async Task UTCID08_GetOrderByIdAsync_OrderStatusIsPaid_ReturnsCorrectStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId, orderStatus: "PAID");

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("PAID", result.Data.OrderStatus);
    }

    [Fact]
    public async Task UTCID09_GetOrderByIdAsync_OrderStatusIsCompleted_ReturnsCorrectStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId, orderStatus: "COMPLETED");

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("COMPLETED", result.Data.OrderStatus);
    }

    [Fact]
    public async Task UTCID10_GetOrderByIdAsync_OrderStatusIsRefunded_ReturnsCorrectStatus()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId, orderStatus: "REFUNDED");

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal("REFUNDED", result.Data.OrderStatus);
    }

    #endregion

    #region Boundary Test Cases (UTCID11 - UTCID16)

    [Fact]
    public async Task UTCID11_GetOrderByIdAsync_OrderWithZeroDetails_ReturnsEmptyDetailsList()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId, details: new List<OrderDetail>());

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Empty(result.Data.Details);
    }

    [Fact]
    public async Task UTCID12_GetOrderByIdAsync_TotalAmountIsZero_MapsZeroPriceSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId, totalAmount: 0m);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal(0m, result.Data.TotalAmount);
    }

    [Fact]
    public async Task UTCID13_GetOrderByIdAsync_LargeTotalAmount_MapsHighValueSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var largeAmount = 99999999m;
        var order = CreateSampleOrder(orderId, traineeId, coachId, totalAmount: largeAmount);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, traineeId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal(largeAmount, result.Data.TotalAmount);
    }

    [Fact]
    public async Task UTCID14_GetOrderByIdAsync_BuyerIdAndCoachIdAreIdentical_ReturnsOrderDtoSuccessfully()
    {
        // Arrange (Edge boundary: User is both buyer and coach)
        var orderId = Guid.NewGuid();
        var sameUserId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, sameUserId, sameUserId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, sameUserId);

        // Assert
        Assert.NotNull(result?.Data);
        Assert.Equal(sameUserId, result.Data.BuyerId);
        Assert.Equal(sameUserId, result.Data.CoachId);
    }

    [Fact]
    public async Task UTCID15_GetOrderByIdAsync_EmptyOrderIdGuid_ThrowsNotFoundException()
    {
        // Arrange
        var emptyOrderId = Guid.Empty;
        var traineeId = Guid.NewGuid();

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(emptyOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetOrderByIdAsync(emptyOrderId, traineeId));

        Assert.Equal("Order not found.", ex.Message);
    }

    [Fact]
    public async Task UTCID16_GetOrderByIdAsync_EmptyCurrentUserIdGuid_ThrowsForbiddenException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, traineeId, coachId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert (currentUserId is Guid.Empty, which matches neither traineeId nor coachId)
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetOrderByIdAsync(orderId, Guid.Empty));

        Assert.Equal("You do not have access to view this order.", ex.Message);
    }

    #endregion

    #region Abnormal Test Cases (UTCID17 - UTCID20)

    [Fact]
    public async Task UTCID17_GetOrderByIdAsync_OrderNotFoundInDatabase_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentOrderId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(nonExistentOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetOrderByIdAsync(nonExistentOrderId, currentUserId));

        Assert.Equal("Order not found.", ex.Message);
    }

    [Fact]
    public async Task UTCID18_GetOrderByIdAsync_UnauthorizedUserAccess_ThrowsForbiddenException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var thirdPartyUserId = Guid.NewGuid(); // Unrelated user
        var order = CreateSampleOrder(orderId, buyerId, coachId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetOrderByIdAsync(orderId, thirdPartyUserId));

        Assert.Equal("You do not have access to view this order.", ex.Message);
    }

    [Fact]
    public async Task UTCID19_GetOrderByIdAsync_DatabaseConnectionFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetOrderByIdAsync(orderId, currentUserId));

        Assert.Equal("Database connection failed.", ex.Message);
    }

    [Fact]
    public async Task UTCID20_GetOrderByIdAsync_OperationCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.GetOrderByIdAsync(orderId, currentUserId, cts.Token));
    }

    [Fact]
    public async Task UTCID21_GetOrderByIdAsync_AdminViewsAnyOrder_ReturnsOrderDtoSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, buyerId, coachId);

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, adminId, isAdmin: true);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(orderId, result.Data!.OrderId);
    }

    [Fact]
    public async Task UTCID22_GetOrderByIdAsync_PackageCoachViewsOrderWhenOrderCoachIdNull_ReturnsOrderDtoSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var orderCoachId = Guid.NewGuid();
        var packageCoachId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId, buyerId, orderCoachId);
        order.CoachId = null;
        order.OrderDetails.First().Package!.CoachId = packageCoachId;

        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, packageCoachId, isAdmin: false);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(orderId, result.Data!.OrderId);
    }

    #endregion
}
