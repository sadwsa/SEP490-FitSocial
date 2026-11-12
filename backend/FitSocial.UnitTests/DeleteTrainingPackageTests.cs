using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Comprehensive Unit Tests for the "Delete Training Package" (Soft Delete) feature (actor: COACH).
/// Covers all 5 test categories:
/// 1. Happy Path: Successful soft delete when coach owns the package, already inactive package idempotence, 200 OK.
/// 2. Negative Case: Package not found, empty package ID, 400 BadRequest when service fails.
/// 3. Boundary Case: Preserving all non-active properties, maintaining existing orders/reviews, empty GUID handling.
/// 4. Exception Case: Database connection exceptions, DbUpdateConcurrencyException, unhandled service exceptions.
/// 5. Authorization Case: [HttpDelete("{id}")] and [Authorize(Roles = "COACH")] attributes, 401 Unauthorized for missing/invalid token,
///    and cross-coach ownership isolation (Coach B attempting to delete Coach A's package returns false and 400 BadRequest).
/// </summary>
public class DeleteTrainingPackageTests
{
    private readonly Mock<ITrainingPackageRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly TrainingPackageService _service;

    public DeleteTrainingPackageTests()
    {
        _mockRepo = new Mock<ITrainingPackageRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();

        _service = new TrainingPackageService(
            _mockRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object);
    }

    #region 1. Happy Path Cases

    [Fact]
    public async Task SoftDeletePackageAsync_WhenCoachOwnsPackage_ShouldSetIsActiveFalseAndSave()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "HIIT Fat Loss 30 Days",
            Price = 1500000m,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SoftDeletePackageAsync(packageId, coachId);

        // Assert
        Assert.True(result);
        Assert.False(package.IsActive);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WhenPackageAlreadyInactive_ShouldStillSucceedAndReturnTrue()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Off-season Strength Plan",
            IsActive = false // Already soft-deleted / inactive
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SoftDeletePackageAsync(packageId, coachId);

        // Assert
        Assert.True(result);
        Assert.False(package.IsActive);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePackage_Controller_WhenCoachIsAuthenticatedAndSuccess_ShouldReturnOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        mockService.Setup(s => s.SoftDeletePackageAsync(packageId, coachId))
            .ReturnsAsync(true);

        // Act
        var result = await controller.DeletePackage(packageId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data);
        Assert.Contains("Package soft deleted successfully", response.Message);
    }

    #endregion

    #region 2. Negative Cases

    [Fact]
    public async Task SoftDeletePackageAsync_WhenPackageNotFound_ShouldReturnFalse()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var nonExistentPackageId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(nonExistentPackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act
        var result = await _service.SoftDeletePackageAsync(nonExistentPackageId, coachId);

        // Assert
        Assert.False(result);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WhenEmptyPackageId_ShouldReturnFalse()
    {
        // Arrange
        var coachId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act
        var result = await _service.SoftDeletePackageAsync(Guid.Empty, coachId);

        // Assert
        Assert.False(result);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePackage_Controller_WhenServiceReturnsFalse_ShouldReturnBadRequest()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        mockService.Setup(s => s.SoftDeletePackageAsync(packageId, coachId))
            .ReturnsAsync(false);

        // Act
        var result = await controller.DeletePackage(packageId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Cannot delete package or package not found/unauthorized.", response.Message);
    }

    #endregion

    #region 3. Boundary Cases

    [Fact]
    public async Task SoftDeletePackageAsync_PreservesAllOtherPackageProperties_OnlyChangesIsActive()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var originalPackage = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Complete Transformation 90 Days",
            Description = "Comprehensive diet and personal coaching",
            Price = 3500000m,
            DurationDays = 90,
            PackageType = "CUSTOM",
            SessionCount = 24,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalPackage);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SoftDeletePackageAsync(packageId, coachId);

        // Assert
        Assert.True(result);
        Assert.False(originalPackage.IsActive);
        Assert.Equal("Complete Transformation 90 Days", originalPackage.Title);
        Assert.Equal("Comprehensive diet and personal coaching", originalPackage.Description);
        Assert.Equal(3500000m, originalPackage.Price);
        Assert.Equal(90, originalPackage.DurationDays);
        Assert.Equal("CUSTOM", originalPackage.PackageType);
        Assert.Equal((short)24, originalPackage.SessionCount);
        Assert.Equal(coachId, originalPackage.CoachId);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WithExistingOrdersAndReviews_PreservesNavigations()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewsList = new List<Review>
        {
            new() { ReviewId = Guid.NewGuid(), PackageId = packageId, Rating = 5, Comment = "Excellent coach!" }
        };
        var orderDetailsList = new List<OrderDetail>
        {
            new() { OrderDetailsId = Guid.NewGuid(), PackageId = packageId, PackagePrice = 1200000m }
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Active Running Plan",
            IsActive = true,
            Reviews = reviewsList,
            OrderDetails = orderDetailsList
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SoftDeletePackageAsync(packageId, coachId);

        // Assert
        Assert.True(result);
        Assert.False(package.IsActive);
        Assert.Single(package.Reviews);
        Assert.Single(package.OrderDetails);
    }

    [Fact]
    public async Task DeletePackage_Controller_WhenCalledWithEmptyGuid_PassesEmptyGuidToServiceAndReturnsBadRequest()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        mockService.Setup(s => s.SoftDeletePackageAsync(Guid.Empty, coachId))
            .ReturnsAsync(false);

        // Act
        var result = await controller.DeletePackage(Guid.Empty);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        mockService.Verify(s => s.SoftDeletePackageAsync(Guid.Empty, coachId), Times.Once);
    }

    #endregion

    #region 4. Exception Cases

    [Fact]
    public async Task SoftDeletePackageAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection timeout."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SoftDeletePackageAsync(packageId, coachId));

        Assert.Equal("Database connection timeout.", ex.Message);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WhenUnitOfWorkThrowsDbUpdateException_PropagatesException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Package to Delete",
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict occurred."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            _service.SoftDeletePackageAsync(packageId, coachId));

        Assert.Equal("Concurrency conflict occurred.", ex.Message);
    }

    [Fact]
    public async Task DeletePackage_Controller_WhenServiceThrowsException_PropagatesException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        mockService.Setup(s => s.SoftDeletePackageAsync(packageId, coachId))
            .ThrowsAsync(new InvalidOperationException("Internal server error"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.DeletePackage(packageId));

        Assert.Equal("Internal server error", ex.Message);
    }

    #endregion

    #region 5. Authorization Cases

    [Fact]
    public void DeletePackage_Endpoint_HasHttpDeleteAttribute_WithIdRoute()
    {
        var method = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.DeletePackage));
        Assert.NotNull(method);

        var httpDeleteAttr = method.GetCustomAttributes(typeof(HttpDeleteAttribute), false)
            .Cast<HttpDeleteAttribute>()
            .FirstOrDefault();

        Assert.NotNull(httpDeleteAttr);
        Assert.Equal("{id}", httpDeleteAttr.Template);
    }

    [Fact]
    public void DeletePackage_Endpoint_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        var method = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.DeletePackage));
        Assert.NotNull(method);

        var authAttr = method.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.Equal("COACH", authAttr.Roles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task DeletePackage_Controller_WhenTokenIsInvalidOrMissing_ShouldReturnUnauthorized(string? invalidUserId)
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var claims = new List<Claim>();
        if (invalidUserId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, invalidUserId));
        }

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await controller.DeletePackage(packageId);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Invalid token or user ID", response.Message);
        mockService.Verify(s => s.SoftDeletePackageAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WhenDifferentCoachAttemptsToDelete_ReturnsFalseEnsuringDataIsolation()
    {
        // Arrange
        var coachAId = Guid.NewGuid(); // Owner of package
        var coachBId = Guid.NewGuid(); // Attacker / Unauthorized Coach
        var packageId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachAId, // Belongs to Coach A
            Title = "Coach A's Elite Package",
            Price = 2000000m,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act: Coach B tries to soft-delete Coach A's package
        var result = await _service.SoftDeletePackageAsync(packageId, coachBId);

        // Assert
        Assert.False(result);
        Assert.True(package.IsActive); // MUST remain TRUE!
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeletePackageAsync_WhenCurrentUserIdIsEmpty_ReturnsFalse()
    {
        // Arrange
        var coachAId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachAId,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.SoftDeletePackageAsync(packageId, Guid.Empty);

        // Assert
        Assert.False(result);
        Assert.True(package.IsActive);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeletePackage_Controller_WhenAnotherCoachAttemptsToDelete_ReturnsBadRequest()
    {
        // Arrange
        var attackerCoachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object, null!);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, attackerCoachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Service returns false due to ownership mismatch
        mockService.Setup(s => s.SoftDeletePackageAsync(packageId, attackerCoachId))
            .ReturnsAsync(false);

        // Act
        var result = await controller.DeletePackage(packageId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Cannot delete package or package not found/unauthorized.", response.Message);
    }

    #endregion
}
