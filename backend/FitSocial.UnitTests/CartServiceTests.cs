using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.Exceptions;
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
    public async Task AddToCartAsync_FirstTimeAddingProduct_ShouldCreateCartItemAndReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var user = new User { UserId = userId, FullName = "Trainee User" };
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "30-Day Weight Loss",
            Price = 500000,
            DurationDays = 30,
            IsActive = true
        };

        var requestDto = new AddToCartRequestDto
        {
            PackageId = packageId,
            Quantity = 1
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _mockCartRepo.Setup(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AddToCartAsync(userId, requestDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal(1, result.Quantity);
        Assert.Equal("30-Day Weight Loss", result.PackageTitle);
        Assert.Equal(500000, result.Price);

        _mockCartRepo.Verify(r => r.AddAsync(It.Is<Cart>(c => c.UserId == userId && c.PackageId == packageId && c.Quantity == 1), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_SecondTimeAddingSameProduct_ShouldIncreaseQuantity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var user = new User { UserId = userId, FullName = "Trainee User" };
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
            Quantity = 2,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var requestDto = new AddToCartRequestDto
        {
            ProductId = packageId,
            Quantity = 3
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCart);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.AddToCartAsync(userId, requestDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, existingCart.Quantity);
        Assert.Equal(5, result.Quantity);
        _mockCartRepo.Verify(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_UnauthenticatedUser_ShouldThrowValidationException()
    {
        // Arrange
        var requestDto = new AddToCartRequestDto { PackageId = Guid.NewGuid(), Quantity = 1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddToCartAsync(Guid.Empty, requestDto));
        Assert.Equal("User is not authenticated.", ex.Message);
    }

    [Fact]
    public async Task AddToCartAsync_EmptyProductId_ShouldThrowValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestDto = new AddToCartRequestDto { Quantity = 1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("Product ID is required.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task AddToCartAsync_InvalidQuantity_ShouldThrowValidationException(int quantity)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var requestDto = new AddToCartRequestDto { PackageId = Guid.NewGuid(), Quantity = quantity };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("Quantity must be greater than 0.", ex.Message);
    }

    [Fact]
    public async Task AddToCartAsync_UserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var requestDto = new AddToCartRequestDto { PackageId = packageId, Quantity = 1 };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("User account not found.", ex.Message);
    }

    [Fact]
    public async Task AddToCartAsync_ProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var user = new User { UserId = userId };
        var requestDto = new AddToCartRequestDto { PackageId = packageId, Quantity = 1 };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("Training package not found.", ex.Message);
    }

    [Fact]
    public async Task AddToCartAsync_ProductInactive_ShouldThrowBusinessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var user = new User { UserId = userId };
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = Guid.NewGuid(),
            Title = "Archived Package",
            IsActive = false
        };
        var requestDto = new AddToCartRequestDto { PackageId = packageId, Quantity = 1 };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("Training package is no longer available.", ex.Message);
    }

    [Fact]
    public async Task AddToCartAsync_CoachAddsOwnPackage_ShouldThrowBusinessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var user = new User { UserId = userId };
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = userId, // Coach is the current user
            Title = "My Own Package",
            IsActive = true
        };
        var requestDto = new AddToCartRequestDto { PackageId = packageId, Quantity = 1 };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.AddToCartAsync(userId, requestDto));
        Assert.Equal("You cannot add your own package to the cart.", ex.Message);
    }

    [Fact]
    public async Task GetCartCountAsync_AuthenticatedUser_ShouldReturnTotalQuantity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCartRepo.Setup(r => r.GetCartCountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var count = await _service.GetCartCountAsync(userId);

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetCartCountAsync_EmptyUserId_ShouldReturnZero()
    {
        // Act
        var count = await _service.GetCartCountAsync(Guid.Empty);

        // Assert
        Assert.Equal(0, count);
    }
}
