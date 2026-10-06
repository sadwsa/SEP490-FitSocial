using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CartServiceTests
{
    private readonly Mock<ICartRepository> _mockCartRepo;
    private readonly Mock<ITrainingPackageRepository> _mockPackageRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CartService _service;

    public CartServiceTests()
    {
        _mockCartRepo = new Mock<ICartRepository>();
        _mockPackageRepo = new Mock<ITrainingPackageRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new CartService(
            _mockCartRepo.Object,
            _mockPackageRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object);
    }

    [Fact]
    public async Task AddToCartAsync_FirstTimeAddingProduct_ShouldCreateCartItemAndReturnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "30-Day Weight Loss",
            Price = 500000,
            DurationDays = 30,
            IsActive = true
        };

        var dto = new AddToCartDto
        {
            PackageId = packageId,
            Quantity = 1
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _mockCartRepo.Setup(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(packageId, result.Data.PackageId);
        Assert.Equal(1, result.Data.Quantity);
        Assert.Equal("30-Day Weight Loss", result.Data.Title);
        Assert.Equal(500000, result.Data.Price);

        _mockCartRepo.Verify(r => r.AddAsync(It.Is<Cart>(c => c.UserId == userId && c.PackageId == packageId && c.Quantity == 1), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_SecondTimeAddingSameProduct_ShouldReturnFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "30-Day Weight Loss",
            Price = 500000,
            DurationDays = 30,
            IsActive = true
        };

        var existingCart = new Cart
        {
            CartId = Guid.NewGuid(),
            UserId = userId,
            PackageId = packageId,
            Quantity = 1,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var dto = new AddToCartDto
        {
            PackageId = packageId,
            Quantity = 1
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCart);

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("already in your shopping cart", result.Message);
        _mockCartRepo.Verify(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddToCartAsync_UnauthenticatedUser_ShouldReturnFail()
    {
        // Arrange
        var dto = new AddToCartDto { PackageId = Guid.NewGuid(), Quantity = 1 };

        // Act
        var result = await _service.AddToCartAsync(Guid.Empty, dto);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddToCartAsync_EmptyPackageId_ShouldReturnFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new AddToCartDto { PackageId = Guid.Empty, Quantity = 1 };

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task AddToCartAsync_InvalidQuantity_ShouldReturnFail(int quantity)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dto = new AddToCartDto { PackageId = Guid.NewGuid(), Quantity = quantity };

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddToCartAsync_PackageNotFound_ShouldReturnFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var dto = new AddToCartDto { PackageId = packageId, Quantity = 1 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Training package does not exist.", result.Message);
    }

    [Fact]
    public async Task AddToCartAsync_ProductInactive_ShouldReturnFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = Guid.NewGuid(),
            Title = "Archived Package",
            IsActive = false
        };
        var dto = new AddToCartDto { PackageId = packageId, Quantity = 1 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("This training package is currently suspended from taking trainees.", result.Message);
    }

    [Fact]
    public async Task AddToCartAsync_CoachAddsOwnPackage_ShouldReturnFail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = userId, // Coach is the current user
            Title = "My Own Package",
            IsActive = true
        };
        var dto = new AddToCartDto { PackageId = packageId, Quantity = 1 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You cannot add your own training package to cart.", result.Message);
    }

    [Fact]
    public async Task GetCartCountAsync_AuthenticatedUser_ShouldReturnTotalQuantity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCartRepo.Setup(r => r.GetCartCountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _service.GetCartCountAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.Data);
    }

    [Fact]
    public async Task GetCartCountAsync_EmptyUserId_ShouldReturnZero()
    {
        // Act
        var result = await _service.GetCartCountAsync(Guid.Empty);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
    }
}
