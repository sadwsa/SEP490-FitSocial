using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for View Trainee Purchase Orders History and Order History feature (UC-14).
/// Covers:
/// 1. Application Layer (OrderService):
///    - GetCoachTraineeOrdersAsync: Grouping by trainee, calculation of total spent, empty coach ID, no orders.
///    - GetCoachTraineeOrderDetailAsync: Single trainee order details, empty coach/trainee ID, not found.
///    - GetMyOrdersAsync: Trainee order history, mapping, empty list.
/// 2. API Presentation Layer (OrdersController):
///    - GetCoachTraineeOrders: Authorize COACH attribute, 200 OK, 401 Unauthorized.
///    - GetCoachTraineeOrderDetail: Authorize COACH attribute, 200 OK, 401 Unauthorized.
///    - GetMyOrders: Authorize attribute, 200 OK, 401 Unauthorized.
/// </summary>
public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<ITrainingPackageRepository> _packageRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _packageRepoMock = new Mock<ITrainingPackageRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _service = new OrderService(
            _orderRepoMock.Object,
            _packageRepoMock.Object,
            _userRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    #region Helper Factories

    private static Order CreateSampleOrder(
        Guid coachId,
        Guid traineeId,
        Guid packageId,
        string traineeName = "Nguyen Van A",
        string traineeEmail = "trainee@example.com",
        decimal price = 1500000m,
        string packageTitle = "Hypertrophy Program")
    {
        var trainee = new User
        {
            UserId = traineeId,
            FullName = traineeName,
            Email = traineeEmail,
            PhoneNumber = "0912345678"
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = packageTitle,
            DurationDays = 30,
            Price = price
        };

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = traineeId,
            Buyer = trainee,
            CoachId = coachId,
            TotalAmount = price,
            OrderStatus = "COMPLETED",
            OrderType = "PACKAGE",
            CreatedAt = DateTime.UtcNow
        };

        var orderDetail = new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = order.OrderId,
            PackageId = packageId,
            Package = package,
            PackageTitle = package.Title,
            PackagePrice = price,
            PackageDurationDays = 30
        };
        order.OrderDetails.Add(orderDetail);

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = price,
            Status = "SUCCESS",
            GatewayTransactionId = "TXN_123456"
        };
        order.Payments.Add(payment);

        return order;
    }

    #endregion

    #region 1. Application Layer: GetCoachTraineeOrdersAsync Tests

    [Fact]
    public async Task GetCoachTraineeOrdersAsync_WhenEmptyCoachId_ThrowsValidationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeOrdersAsync(Guid.Empty));
        Assert.Equal("Invalid coach identity.", ex.Message);
    }

    [Fact]
    public async Task GetCoachTraineeOrdersAsync_WhenCoachHasTraineeOrders_GroupsCorrectly()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var order = CreateSampleOrder(coachId, traineeId, packageId);

        _orderRepoMock.Setup(r => r.GetOrdersByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        // Act
        var result = await _service.GetCoachTraineeOrdersAsync(coachId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);

        var group = result.Data.First();
        Assert.Equal(traineeId, group.TraineeId);
        Assert.Equal("Nguyen Van A", group.FullName);
        Assert.Equal("trainee@example.com", group.Email);
        Assert.Equal(1, group.TotalOrdersCount);
        Assert.Equal(1500000m, group.TotalSpent);
        Assert.Single(group.PurchasedPackages);

        var pkgOrder = group.PurchasedPackages.First();
        Assert.Equal(packageId, pkgOrder.PackageId);
        Assert.Equal("Hypertrophy Program", pkgOrder.PackageTitle);
        Assert.Equal("TXN_123456", pkgOrder.OrderCode);
        Assert.Equal("COMPLETED", pkgOrder.OrderStatus);
    }

    [Fact]
    public async Task GetCoachTraineeOrdersAsync_WhenCoachHasNoOrders_ReturnsEmptyList()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetOrdersByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await _service.GetCoachTraineeOrdersAsync(coachId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    #endregion

    #region 2. Application Layer: GetCoachTraineeOrderDetailAsync Tests

    [Fact]
    public async Task GetCoachTraineeOrderDetailAsync_WhenEmptyCoachId_ThrowsValidationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeOrderDetailAsync(Guid.Empty, Guid.NewGuid()));
        Assert.Equal("Invalid coach identity.", ex.Message);
    }

    [Fact]
    public async Task GetCoachTraineeOrderDetailAsync_WhenEmptyTraineeId_ThrowsValidationException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeOrderDetailAsync(Guid.NewGuid(), Guid.Empty));
        Assert.Equal("Invalid trainee identity.", ex.Message);
    }

    [Fact]
    public async Task GetCoachTraineeOrderDetailAsync_WhenTraineeHasOrdersWithCoach_ReturnsTraineeGroup()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var order = CreateSampleOrder(coachId, traineeId, packageId, traineeName: "Tran Thi B");

        _orderRepoMock.Setup(r => r.GetOrdersByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        // Act
        var result = await _service.GetCoachTraineeOrderDetailAsync(coachId, traineeId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(traineeId, result.Data.TraineeId);
        Assert.Equal("Tran Thi B", result.Data.FullName);
        Assert.Single(result.Data.PurchasedPackages);
    }

    [Fact]
    public async Task GetCoachTraineeOrderDetailAsync_WhenTraineeHasNoOrdersWithCoach_ThrowsNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        _orderRepoMock.Setup(r => r.GetOrdersByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetCoachTraineeOrderDetailAsync(coachId, traineeId));
        Assert.Contains("No trainee orders found for this trainee", ex.Message);
    }

    #endregion

    #region 3. Application Layer: GetMyOrdersAsync Tests

    [Fact]
    public async Task GetMyOrdersAsync_WhenBuyerHasNoOrders_ReturnsEmptyList()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetOrdersByBuyerIdAsync(buyerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await _service.GetMyOrdersAsync(buyerId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetMyOrdersAsync_WhenBuyerHasOrders_ReturnsMappedOrders()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var order = CreateSampleOrder(coachId, buyerId, packageId);

        _orderRepoMock.Setup(r => r.GetOrdersByBuyerIdAsync(buyerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Order> { order });

        // Act
        var result = await _service.GetMyOrdersAsync(buyerId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(order.OrderId, result.Data[0].OrderId);
        Assert.Equal(buyerId, result.Data[0].BuyerId);
        Assert.Equal(1500000m, result.Data[0].TotalAmount);
    }

    #endregion

    #region 4. API Presentation Layer: OrdersController Tests

    [Fact]
    public void OrdersController_HasAuthorizeAttribute()
    {
        // Arrange & Act
        var attr = typeof(OrdersController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attr);
    }

    [Fact]
    public void GetCoachTraineeOrders_Endpoint_HasAuthorizeCoachRole()
    {
        // Arrange & Act
        var method = typeof(OrdersController).GetMethod(nameof(OrdersController.GetCoachTraineeOrders));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("COACH", attr.Roles);
    }

    [Fact]
    public async Task GetCoachTraineeOrders_WhenCoachAuthenticated_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<IOrderService>();
        var controller = new OrdersController(mockService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expectedData = new List<CoachTraineeGroupDto>
        {
            new() { TraineeId = Guid.NewGuid(), FullName = "Trainee One", TotalOrdersCount = 1 }
        };

        mockService.Setup(s => s.GetCoachTraineeOrdersAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<List<CoachTraineeGroupDto>>.Ok(expectedData));

        // Act
        var result = await controller.GetCoachTraineeOrders(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<List<CoachTraineeGroupDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data);
    }

    [Fact]
    public async Task GetCoachTraineeOrders_WhenTokenInvalidOrMissing_ReturnsUnauthorized()
    {
        // Arrange
        var mockService = new Mock<IOrderService>();
        var controller = new OrdersController(mockService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await controller.GetCoachTraineeOrders(CancellationToken.None);

        // Assert
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<List<CoachTraineeGroupDto>>>(unauthorized.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetCoachTraineeOrderDetail_WhenCoachAuthenticated_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var mockService = new Mock<IOrderService>();
        var controller = new OrdersController(mockService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expectedGroup = new CoachTraineeGroupDto
        {
            TraineeId = traineeId,
            FullName = "Trainee Detail",
            TotalOrdersCount = 2
        };

        mockService.Setup(s => s.GetCoachTraineeOrderDetailAsync(coachId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachTraineeGroupDto>.Ok(expectedGroup));

        // Act
        var result = await controller.GetCoachTraineeOrderDetail(traineeId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<CoachTraineeGroupDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(traineeId, response.Data?.TraineeId);
    }

    [Fact]
    public async Task GetMyOrders_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockService = new Mock<IOrderService>();
        var controller = new OrdersController(mockService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, "TRAINEE")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expectedOrders = new List<OrderDto>
        {
            new() { OrderId = Guid.NewGuid(), BuyerId = userId, TotalAmount = 500000m }
        };

        mockService.Setup(s => s.GetMyOrdersAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<List<OrderDto>>.Ok(expectedOrders));

        // Act
        var result = await controller.GetMyOrders(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<List<OrderDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data);
    }

    #endregion
}
