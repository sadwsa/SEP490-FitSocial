using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Cart;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Contains("already in your cart", result.Message, StringComparison.OrdinalIgnoreCase);
        _mockCartRepo.Verify(r => r.AddAsync(It.IsAny<Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddToCartAsync_DifferentPackage_ShouldSuccessfullyAddWithQuantityOne()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId2 = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package2 = new TrainingPackage
        {
            PackageId = packageId2,
            CoachId = coachId,
            Title = "60-Day Muscle Building",
            Price = 900000,
            DurationDays = 60,
            IsActive = true
        };

        var dto = new AddToCartDto
        {
            PackageId = packageId2,
            Quantity = 3 // Even if requested quantity is 3, enforced to 1
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package2);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId2, It.IsAny<CancellationToken>()))
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
        Assert.Equal(packageId2, result.Data.PackageId);
        Assert.Equal(1, result.Data.Quantity);
        _mockCartRepo.Verify(r => r.AddAsync(It.Is<Cart>(c => c.UserId == userId && c.PackageId == packageId2 && c.Quantity == 1), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public async Task AddToCartAsync_NullDto_ShouldReturnFail()
    {
        // Act (Negative case: null request)
        var result = await _service.AddToCartAsync(Guid.NewGuid(), null!);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Invalid training package information", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddToCartAsync_PackageIsActiveNull_ShouldTreatAsActiveAndSucceed()
    {
        // Arrange (Boundary case: IsActive is null, defaults to active)
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Package with Null IsActive",
            Price = 300000,
            DurationDays = 14,
            IsActive = null // Boundary: null active flag
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var dto = new AddToCartDto
        {
            PackageId = packageId,
            Quantity = 1
        };

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(packageId, result.Data.PackageId);
    }

    [Fact]
    public async Task AddToCartAsync_WhenDatabaseSaveThrowsException_ShouldReturnFail()
    {
        // Arrange (Exception case: DB failure)
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Valid Package",
            Price = 400000,
            IsActive = true
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockCartRepo.Setup(r => r.GetCartItemAsync(userId, packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB deadlock detected"));

        var dto = new AddToCartDto
        {
            PackageId = packageId,
            Quantity = 1
        };

        // Act
        var result = await _service.AddToCartAsync(userId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error adding to cart", result.Message);
    }

    #region View Cart Tests - Happy Path

    [Fact]
    public async Task GetCartAsync_WithValidItems_ShouldCalculateSubtotalAndReturnCartSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Trainee };

        var pkg1 = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "Weight Loss 4 Weeks",
            DurationDays = 28,
            Price = 500000,
            IsActive = true,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                ExperienceYears = 5,
                Coach = new User { UserId = coachId, FullName = "Coach John", AvatarUrl = "https://example.com/avatar.jpg" },
                Reviews = new List<Review>
                {
                    new Review { Rating = 5 },
                    new Review { Rating = 5 },
                    new Review { Rating = 4 }
                }
            }
        };

        var pkg2 = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Yoga 10 Days",
            DurationDays = 10,
            Price = 300000,
            IsActive = true,
            Coach = new CoachProfile
            {
                ExperienceYears = 3,
                Coach = new User { FullName = "Coach Sarah" }
            }
        };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = pkg1.PackageId, Package = pkg1, Quantity = 1, CreatedAt = DateTime.UtcNow },
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = pkg2.PackageId, Package = pkg2, Quantity = 1, CreatedAt = DateTime.UtcNow }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Cart retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalItems);
        Assert.Equal(2, result.Data.ValidItemsCount);
        Assert.Equal(800000, result.Data.Subtotal);
        Assert.Equal(800000, result.Data.TotalPrice);
        Assert.Equal(32.00m, result.Data.ApproxUsd); // 800000 / 25000 = 32
        Assert.False(result.Data.HasUnavailableItems);
        Assert.False(result.Data.IsCoach);
        Assert.StartsWith("FS-", result.Data.TemporaryOrderCode);

        var item1 = result.Data.Items[0];
        Assert.Equal("Weight Loss 4 Weeks", item1.Title);
        Assert.Equal("4 Weeks (28 Days)", item1.DurationLabel);
        Assert.Equal("Coach John", item1.CoachName);
        Assert.Equal(4.7, item1.AverageRating);
        Assert.Equal(3, item1.ReviewCount);
        Assert.True(item1.IsAvailable);
        Assert.Null(item1.UnavailableReason);

        var item2 = result.Data.Items[1];
        Assert.Equal("10 Days", item2.DurationLabel);
        Assert.True(item2.IsAvailable);
    }

    [Fact]
    public async Task GetCartAsync_WhenUserIsCoach_ShouldFlagIsCoachTrue()
    {
        // Arrange
        var coachUserId = Guid.NewGuid();
        var coachUser = new User { UserId = coachUserId, RoleCode = RoleConstants.Coach };

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(coachUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cart>());

        // Act
        var result = await _service.GetCartAsync(coachUserId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsCoach);
    }

    [Fact]
    public async Task GetCartCountAsync_ValidUserId_ShouldReturnCorrectCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCartRepo.Setup(r => r.GetCartCountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _service.GetCartCountAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(5, result.Data);
    }

    [Fact]
    public async Task CartsController_GetCart_WhenUserIsAuthenticated_ShouldReturn200Ok()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();
        var summaryDto = new CartSummaryDto { TotalItems = 1, Subtotal = 500000 };

        cartServiceMock.Setup(s => s.GetCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CartSummaryDto>.Ok(summaryDto, "Cart retrieved successfully."));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.GetCart(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<CartSummaryDto>>(okResult.Value);
        Assert.True(body.Success);
    }

    [Fact]
    public async Task CartsController_GetCartCount_WhenUserIsAuthenticated_ShouldReturn200Ok()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();
        cartServiceMock.Setup(s => s.GetCartCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<int>.Ok(3));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.GetCartCount(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<int>>(okResult.Value);
        Assert.Equal(3, body.Data);
    }

    #endregion

    #region View Cart Tests - Negative Cases

    [Fact]
    public async Task GetCartAsync_WhenCartIsEmpty_ShouldReturnZeroTotalSummary()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee });
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Cart>());

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalItems);
        Assert.Equal(0, result.Data.ValidItemsCount);
        Assert.Equal(0, result.Data.Subtotal);
        Assert.Equal(0, result.Data.TotalPrice);
        Assert.False(result.Data.HasUnavailableItems);
    }

    [Fact]
    public async Task GetCartCountAsync_WhenUserIdIsEmptyGuid_ShouldReturnZeroWithoutCallingRepository()
    {
        // Act
        var result = await _service.GetCartCountAsync(Guid.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
        _mockCartRepo.Verify(r => r.GetCartCountByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartsController_GetCart_WhenUserUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
            }
        };

        // Act
        var actionResult = await controller.GetCart(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CartsController_GetCartCount_WhenUserUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
            }
        };

        // Act
        var actionResult = await controller.GetCartCount(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CartsController_GetCart_WhenServiceReturnsError_ShouldReturn400BadRequest()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();

        cartServiceMock.Setup(s => s.GetCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CartSummaryDto>.Fail("Error loading cart: DB failure"));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.GetCart(CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    #endregion

    #region View Cart Tests - Boundary Cases

    [Fact]
    public async Task GetCartAsync_WhenCartItemHasCoachOwnPackage_ShouldMarkAsUnavailable()
    {
        // Arrange: User is coach and has their own package in cart (e.g. from trainee time)
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Coach };

        var ownPackage = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = userId, // Own package!
            Title = "My Own Package",
            Price = 1000000,
            IsActive = true
        };

        var otherPackage = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Other Package",
            Price = 400000,
            IsActive = true
        };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = ownPackage.PackageId, Package = ownPackage, Quantity = 1 },
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = otherPackage.PackageId, Package = otherPackage, Quantity = 1 }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalItems);
        Assert.Equal(1, result.Data.ValidItemsCount); // Only otherPackage is valid
        Assert.Equal(400000, result.Data.Subtotal); // Only valid items summed
        Assert.True(result.Data.HasUnavailableItems);

        var ownItem = result.Data.Items.First(i => i.PackageId == ownPackage.PackageId);
        Assert.False(ownItem.IsAvailable);
        Assert.Equal("Your own training package - Cannot purchase", ownItem.UnavailableReason);
    }

    [Fact]
    public async Task GetCartAsync_WhenCartItemIsInactive_ShouldMarkAsUnavailable()
    {
        // Arrange: Package has IsActive = false
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Trainee };

        var inactivePackage = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Suspended Package",
            Price = 600000,
            IsActive = false // Inactive!
        };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = inactivePackage.PackageId, Package = inactivePackage, Quantity = 1 }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalItems);
        Assert.Equal(0, result.Data.ValidItemsCount);
        Assert.Equal(0, result.Data.Subtotal);
        Assert.True(result.Data.HasUnavailableItems);

        var inactiveItem = result.Data.Items[0];
        Assert.False(inactiveItem.IsAvailable);
        Assert.Equal("This package is currently suspended from taking new trainees", inactiveItem.UnavailableReason);
    }

    [Fact]
    public async Task GetCartAsync_WhenDurationDaysIsNull_ShouldDefaultTo30Days()
    {
        // Arrange: DurationDays is null
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Trainee };

        var package = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = Guid.NewGuid(),
            Title = "Package Without Explicit Duration",
            DurationDays = null,
            Price = 500000,
            IsActive = true
        };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = package.PackageId, Package = package, Quantity = 1 }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("30 Days", result.Data!.Items[0].DurationLabel);
    }

    [Fact]
    public async Task GetCartAsync_WhenCartEntityHasNullPackage_ShouldSkipGracefully()
    {
        // Arrange: Cart entity where navigation property Package is null
        var userId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Trainee };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = Guid.NewGuid(), Package = null!, Quantity = 1 }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalItems);
    }

    [Fact]
    public async Task GetCartAsync_WhenCoachReviewsHaveNoValidRatings_ShouldUseDefaultRating()
    {
        // Arrange: Reviews collection contains reviews where Rating is null
        var userId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var user = new User { UserId = userId, RoleCode = RoleConstants.Trainee };

        var package = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "Package",
            Price = 200000,
            IsActive = true,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Reviews = new List<Review>
                {
                    new Review { Rating = null }
                }
            }
        };

        var cartItems = new List<Cart>
        {
            new Cart { CartId = Guid.NewGuid(), UserId = userId, PackageId = package.PackageId, Package = package, Quantity = 1 }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartItems);

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(4.9, result.Data!.Items[0].AverageRating);
    }

    #endregion

    #region View Cart Tests - Exception Cases

    [Fact]
    public async Task GetCartAsync_WhenUserRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB timeout reading user"));

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error loading cart: DB timeout reading user", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCartAsync_WhenCartRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = userId, RoleCode = RoleConstants.Trainee });
        _mockCartRepo.Setup(r => r.GetCartByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network connectivity dropped"));

        // Act
        var result = await _service.GetCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error loading cart: Network connectivity dropped", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCartAsync_WhenCancellationRequested_ShouldReturnFailureResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.GetCartAsync(userId, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error loading cart", result.Message);
    }

    [Fact]
    public async Task GetCartCountAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCartRepo.Setup(r => r.GetCartCountByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error while counting"));

        // Act
        var result = await _service.GetCartCountAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error counting cart items: Database error while counting", result.Message);
    }

    #endregion

    #region Remove from Cart Tests - Happy Path

    [Fact]
    public async Task RemoveCartItemAsync_ValidCartItemAndOwner_ShouldRemoveSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cart = new Cart
        {
            CartId = cartId,
            UserId = userId,
            PackageId = Guid.NewGuid(),
            Quantity = 1
        };

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.RemoveCartItemAsync(userId, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Removed package from cart successfully.", result.Message);

        _mockCartRepo.Verify(r => r.Remove(cart), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearCartAsync_ValidUser_ShouldClearAllItemsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.ClearCartAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ClearCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Cart cleared successfully.", result.Message);

        _mockCartRepo.Verify(r => r.ClearCartAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CartsController_RemoveCartItem_WhenAuthenticatedAndSuccessful_ShouldReturn200Ok()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        cartServiceMock.Setup(s => s.RemoveCartItemAsync(userId, cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<bool>.Ok(true, "Removed package from cart successfully."));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.RemoveCartItem(cartId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<bool>>(okResult.Value);
        Assert.True(body.Success);
        Assert.True(body.Data);
    }

    [Fact]
    public async Task CartsController_ClearCart_WhenAuthenticatedAndSuccessful_ShouldReturn200Ok()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();

        cartServiceMock.Setup(s => s.ClearCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<bool>.Ok(true, "Cart cleared successfully."));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.ClearCart(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<ApiResponseDto<bool>>(okResult.Value);
        Assert.True(body.Success);
        Assert.True(body.Data);
    }

    #endregion

    #region Remove from Cart Tests - Negative Cases

    [Fact]
    public async Task RemoveCartItemAsync_WhenCartItemDoesNotExist_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        // Act
        var result = await _service.RemoveCartItemAsync(userId, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("Cart item does not exist or does not belong to you.", result.Message);

        _mockCartRepo.Verify(r => r.Remove(It.IsAny<Cart>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveCartItemAsync_WhenCartItemBelongsToDifferentUser_ShouldReturnFailureResult()
    {
        // Arrange
        var callingUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var cart = new Cart
        {
            CartId = cartId,
            UserId = ownerUserId, // Belongs to different user!
            PackageId = Guid.NewGuid()
        };

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        // Act
        var result = await _service.RemoveCartItemAsync(callingUserId, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("Cart item does not exist or does not belong to you.", result.Message);

        _mockCartRepo.Verify(r => r.Remove(It.IsAny<Cart>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CartsController_RemoveCartItem_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
            }
        };

        // Act
        var actionResult = await controller.RemoveCartItem(Guid.NewGuid(), CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CartsController_ClearCart_WhenUnauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no claims
            }
        };

        // Act
        var actionResult = await controller.ClearCart(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CartsController_RemoveCartItem_WhenServiceFails_ShouldReturn400BadRequest()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        cartServiceMock.Setup(s => s.RemoveCartItemAsync(userId, cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<bool>.Fail("Cart item does not exist or does not belong to you."));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.RemoveCartItem(cartId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task CartsController_ClearCart_WhenServiceFails_ShouldReturn400BadRequest()
    {
        // Arrange
        var cartServiceMock = new Mock<ICartService>();
        var userId = Guid.NewGuid();

        cartServiceMock.Setup(s => s.ClearCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<bool>.Fail("Error clearing cart: DB error"));

        var controller = new CartsController(cartServiceMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                }, "TestAuth"))
            }
        };

        // Act
        var actionResult = await controller.ClearCart(CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);
    }

    #endregion

    #region Remove from Cart Tests - Boundary Cases

    [Fact]
    public async Task RemoveCartItemAsync_WithEmptyGuidCartId_WhenNotFound_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCartRepo.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cart?)null);

        // Act
        var result = await _service.RemoveCartItemAsync(userId, Guid.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Cart item does not exist or does not belong to you.", result.Message);
    }

    [Fact]
    public async Task RemoveCartItemAsync_WithEmptyGuidUserId_ShouldPreventUnauthorizedRemoval()
    {
        // Arrange
        var cartId = Guid.NewGuid();
        var validOwnerId = Guid.NewGuid();
        var cart = new Cart { CartId = cartId, UserId = validOwnerId };

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        // Act: User passes Guid.Empty
        var result = await _service.RemoveCartItemAsync(Guid.Empty, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Cart item does not exist or does not belong to you.", result.Message);
        _mockCartRepo.Verify(r => r.Remove(It.IsAny<Cart>()), Times.Never);
    }

    #endregion

    #region Remove from Cart Tests - Exception Cases

    [Fact]
    public async Task RemoveCartItemAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection timeout"));

        // Act
        var result = await _service.RemoveCartItemAsync(userId, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error removing package from cart: DB connection timeout", result.Message);
    }

    [Fact]
    public async Task RemoveCartItemAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        var cart = new Cart { CartId = cartId, UserId = userId };

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB constraint error during commit"));

        // Act
        var result = await _service.RemoveCartItemAsync(userId, cartId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error removing package from cart: DB constraint error during commit", result.Message);
    }

    [Fact]
    public async Task RemoveCartItemAsync_WhenCancellationRequested_ShouldReturnFailureResult()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var userId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.GetByIdAsync(cartId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.RemoveCartItemAsync(userId, cartId, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error removing package from cart", result.Message);
    }

    [Fact]
    public async Task ClearCartAsync_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.ClearCartAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection dropped"));

        // Act
        var result = await _service.ClearCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error clearing cart: DB connection dropped", result.Message);
    }

    [Fact]
    public async Task ClearCartAsync_WhenUnitOfWorkThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockCartRepo.Setup(r => r.ClearCartAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Deadlock detected during commit"));

        // Act
        var result = await _service.ClearCartAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error clearing cart: Deadlock detected during commit", result.Message);
    }

    #endregion
}