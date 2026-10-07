using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

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

    [Fact]
    public async Task GetCoachTraineeOrdersAsync_WhenEmptyCoachId_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.GetCoachTraineeOrdersAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetCoachTraineeOrdersAsync_WhenCoachHasTraineeOrders_GroupsCorrectly()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var trainee = new User
        {
            UserId = traineeId,
            FullName = "Nguyen Van A",
            Email = "trainee@example.com",
            PhoneNumber = "0912345678"
        };

        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Hypertrophy Program",
            DurationDays = 30,
            Price = 1500000m
        };

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = traineeId,
            Buyer = trainee,
            CoachId = coachId,
            TotalAmount = 1500000m,
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
            PackagePrice = 1500000m,
            PackageDurationDays = 30
        };
        order.OrderDetails.Add(orderDetail);

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = 1500000m,
            Status = "SUCCESS",
            GatewayTransactionId = "123456789"
        };
        order.Payments.Add(payment);

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
        Assert.Equal("123456789", pkgOrder.OrderCode);
        Assert.Equal("COMPLETED", pkgOrder.OrderStatus);
    }
}
