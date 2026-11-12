using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
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
/// Comprehensive Unit tests for Create Training Package and Update Training Package features (actor: COACH).
/// Covers:
/// 1. Application Layer (TrainingPackageService):
///    - CreatePackageAsync: field persistence, media creation, packageType normalization, whitespace filtering.
///    - UpdatePackageAsync: successful update, partial update, media replacement, not found,
///      and coach ownership isolation (blocking Coach B from updating Coach A's package).
/// 2. API Presentation Layer (TrainingPackagesController):
///    - CreatePackage: Authorize COACH attribute, 201 CreatedAtAction, 401 Unauthorized.
///    - UpdatePackage: Authorize COACH attribute, 200 OK, 404 NotFound / Unauthorized, 401 Unauthorized.
/// </summary>
public class TrainingPackageServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IReviewMediaRepository> _mockReviewMediaRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly TrainingPackageService _service;

    public TrainingPackageServiceTests()
    {
        _mockRepo = new Mock<ITrainingPackageRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockImageService = new Mock<IImageService>();
        _mockReviewMediaRepo = new Mock<IReviewMediaRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockEmailService = new Mock<IEmailService>();
        _service = new TrainingPackageService(
            _mockRepo.Object, _mockUow.Object, _mockReviewRepo.Object, _mockOrderRepo.Object,
            _mockReviewMediaRepo.Object, _mockUserRepo.Object, _mockNotificationRepo.Object, _mockEmailService.Object);
    }

    #region 1. Application Layer: CreatePackageAsync Tests

    [Fact]
    public async Task CreatePackageAsync_With4NewFields_ShouldPersistAndReturnDto()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CreateTrainingPackageDto
        {
            Title = "Advanced Hypertrophy Program",
            Description = "12-week muscle building plan",
            Price = 1500000,
            DurationDays = 90,
            SessionCount = 36,
            MinAge = 18,
            TargetAudience = "Intermediate to advanced lifters",
            IsActive = true
        };

        _mockRepo.Setup(r => r.AddAsync(It.IsAny<TrainingPackage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreatePackageAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal(dto.Title, result.Title);
        Assert.Equal(dto.Description, result.Description);
        Assert.Equal(dto.SessionCount, result.SessionCount);
        Assert.Equal(dto.MinAge, result.MinAge);
        Assert.Equal(dto.TargetAudience, result.TargetAudience);
        Assert.True(result.IsActive);
        _mockRepo.Verify(r => r.AddAsync(It.Is<TrainingPackage>(p =>
            p.Description == dto.Description &&
            p.SessionCount == dto.SessionCount &&
            p.MinAge == dto.MinAge &&
            p.TargetAudience == dto.TargetAudience), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePackageAsync_WithImageUrls_ShouldCreateMediaEntities()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CreateTrainingPackageDto
        {
            Title = "Yoga Essentials",
            Price = 500000,
            DurationDays = 30,
            ImageUrls = new List<string> { "https://res.cloudinary.com/demo/image1.jpg", "https://res.cloudinary.com/demo/image2.jpg" }
        };

        TrainingPackage? capturedPackage = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<TrainingPackage>(), It.IsAny<CancellationToken>()))
            .Callback<TrainingPackage, CancellationToken>((pkg, _) => capturedPackage = pkg)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreatePackageAsync(coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedPackage);
        Assert.Equal(2, capturedPackage.Media.Count);
        Assert.Contains(capturedPackage.Media, m => m.MediaUrl == "https://res.cloudinary.com/demo/image1.jpg");
        Assert.Contains(capturedPackage.Media, m => m.MediaUrl == "https://res.cloudinary.com/demo/image2.jpg");
    }

    [Theory]
    [InlineData("workout", "WORKOUT")]
    [InlineData("meal ", "MEAL")]
    [InlineData("", "BOTH")]
    [InlineData("   ", "BOTH")]
    [InlineData(null, "BOTH")]
    public async Task CreatePackageAsync_NormalizesPackageTypeCorrectly(string? inputType, string expectedType)
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CreateTrainingPackageDto
        {
            Title = "Package with custom type",
            Price = 1000000,
            DurationDays = 30,
            PackageType = inputType
        };

        TrainingPackage? captured = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<TrainingPackage>(), It.IsAny<CancellationToken>()))
            .Callback<TrainingPackage, CancellationToken>((pkg, _) => captured = pkg)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreatePackageAsync(coachId, dto);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(expectedType, captured.PackageType);
    }

    [Fact]
    public async Task CreatePackageAsync_FiltersOutEmptyOrWhitespaceImageUrls()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var dto = new CreateTrainingPackageDto
        {
            Title = "Media Filter Test",
            Price = 800000,
            DurationDays = 15,
            ImageUrls = new List<string> { "https://example.com/valid.jpg", "   ", "", "  https://example.com/valid2.jpg  " }
        };

        TrainingPackage? captured = null;
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<TrainingPackage>(), It.IsAny<CancellationToken>()))
            .Callback<TrainingPackage, CancellationToken>((pkg, _) => captured = pkg)
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreatePackageAsync(coachId, dto);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(2, captured.Media.Count);
        Assert.Equal("https://example.com/valid.jpg", captured.Media.ElementAt(0).MediaUrl);
        Assert.Equal("https://example.com/valid2.jpg", captured.Media.ElementAt(1).MediaUrl);
        Assert.Equal((short)0, captured.Media.ElementAt(0).SortOrder);
        Assert.Equal((short)1, captured.Media.ElementAt(1).SortOrder);
    }

    #endregion

    #region 2. Application Layer: UpdatePackageAsync Tests

    [Fact]
    public async Task UpdatePackageAsync_ShouldUpdateFieldsProperly()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var existing = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Old Title",
            Price = 1000000,
            DurationDays = 30,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = new UpdateTrainingPackageDto
        {
            Title = "New Title",
            Description = "Updated Description",
            Price = 1200000,
            DurationDays = 45,
            SessionCount = 20,
            MinAge = 16,
            TargetAudience = "Beginners",
            PackageType = "WORKOUT",
            IsActive = false
        };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Title", result!.Title);
        Assert.Equal("Updated Description", result.Description);
        Assert.Equal(1200000m, result.Price);
        Assert.Equal(45, result.DurationDays);
        Assert.Equal((short)20, result.SessionCount);
        Assert.Equal((short)16, result.MinAge);
        Assert.Equal("Beginners", result.TargetAudience);
        Assert.Equal("WORKOUT", result.PackageType);
        Assert.False(result.IsActive);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePackageAsync_WhenPackageNotFound_ShouldReturnNull()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        var updateDto = new UpdateTrainingPackageDto { Title = "Updated" };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachId, updateDto);

        // Assert
        Assert.Null(result);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePackageAsync_WhenCoachDoesNotOwnPackage_ShouldReturnNullEnsuringSecurityIsolation()
    {
        // Arrange
        // Security check: Coach B tries to modify Coach A's package
        var coachAId = Guid.NewGuid();
        var coachBId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var packageOfCoachA = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachAId, // Belongs to Coach A
            Title = "Coach A's Package",
            Price = 1000000m,
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageOfCoachA);

        var updateDto = new UpdateTrainingPackageDto
        {
            Title = "Hacked Title by Coach B",
            Price = 50000m
        };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachBId, updateDto);

        // Assert
        Assert.Null(result); // Forbidden / unauthorized
        Assert.Equal("Coach A's Package", packageOfCoachA.Title); // Unmodified
        Assert.Equal(1000000m, packageOfCoachA.Price);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdatePackageAsync_PartialUpdate_PreservesUnspecifiedFields()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var existing = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Original Title",
            Description = "Original Description",
            Price = 2000000m,
            DurationDays = 60,
            SessionCount = 24,
            MinAge = 18,
            TargetAudience = "Pro athletes",
            PackageType = "BOTH",
            IsActive = true
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Only update Price, leaving other optional fields null
        var updateDto = new UpdateTrainingPackageDto
        {
            Price = 2500000m
        };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Original Title", result!.Title);
        Assert.Equal("Original Description", result.Description);
        Assert.Equal(2500000m, result.Price);
        Assert.Equal(60, result.DurationDays);
        Assert.Equal((short)18, result.MinAge);
        Assert.Equal("Pro athletes", result.TargetAudience);
        Assert.Equal("BOTH", result.PackageType);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdatePackageAsync_WithNewImageUrls_ReplacesOldMediaCompletely()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var existing = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Plan with old media",
            Price = 1000000m,
            Media = new List<TrainingPackageMedium>
            {
                new() { MediaId = Guid.NewGuid(), MediaUrl = "https://example.com/old1.jpg", SortOrder = 0 }
            }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updateDto = new UpdateTrainingPackageDto
        {
            ImageUrls = new List<string> { "https://example.com/new1.jpg", "https://example.com/new2.jpg" }
        };

        // Act
        var result = await _service.UpdatePackageAsync(packageId, coachId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, existing.Media.Count);
        Assert.DoesNotContain(existing.Media, m => m.MediaUrl == "https://example.com/old1.jpg");
        Assert.Contains(existing.Media, m => m.MediaUrl == "https://example.com/new1.jpg");
        Assert.Contains(existing.Media, m => m.MediaUrl == "https://example.com/new2.jpg");
    }

    #endregion

    #region 3. API Presentation Layer: Controller Tests

    [Fact]
    public void CreatePackage_Endpoint_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        // Arrange & Act
        var method = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.CreatePackage));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("COACH", attr.Roles);
    }

    [Fact]
    public async Task CreatePackage_WhenCoachIsAuthenticated_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var newPackageId = Guid.NewGuid();
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

        var dto = new CreateTrainingPackageDto { Title = "New Bootcamp", Price = 1200000m };
        var expectedResponse = new TrainingPackageResponseDto
        {
            PackageId = newPackageId,
            CoachId = coachId,
            Title = "New Bootcamp",
            Price = 1200000m
        };

        mockService.Setup(s => s.CreatePackageAsync(coachId, dto))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.CreatePackage(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(newPackageId, response.Data.PackageId);
        mockService.Verify(s => s.CreatePackageAsync(coachId, dto), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task CreatePackage_WhenTokenIsInvalidOrMissing_ShouldReturnUnauthorized(string? invalidUserId)
    {
        // Arrange
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

        var dto = new CreateTrainingPackageDto { Title = "Bootcamp" };

        // Act
        var result = await controller.CreatePackage(dto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        mockService.Verify(s => s.CreatePackageAsync(It.IsAny<Guid>(), It.IsAny<CreateTrainingPackageDto>()), Times.Never);
    }

    [Fact]
    public void UpdatePackage_Endpoint_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        // Arrange & Act
        var method = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.UpdatePackage));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(attr);
        Assert.Equal("COACH", attr.Roles);
    }

    [Fact]
    public async Task UpdatePackage_WhenCoachIsAuthenticatedAndSuccess_ShouldReturnOk()
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

        var dto = new UpdateTrainingPackageDto { Title = "Updated Bootcamp" };
        var expectedResponse = new TrainingPackageResponseDto
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Updated Bootcamp"
        };

        mockService.Setup(s => s.UpdatePackageAsync(packageId, coachId, dto))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await controller.UpdatePackage(packageId, dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Updated Bootcamp", response.Data?.Title);
        mockService.Verify(s => s.UpdatePackageAsync(packageId, coachId, dto), Times.Once);
    }

    [Fact]
    public async Task UpdatePackage_WhenNotFoundOrUnauthorized_ShouldReturnNotFound()
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

        var dto = new UpdateTrainingPackageDto { Title = "Updated" };

        mockService.Setup(s => s.UpdatePackageAsync(packageId, coachId, dto))
            .ReturnsAsync((TrainingPackageResponseDto?)null);

        // Act
        var result = await controller.UpdatePackage(packageId, dto);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(notFoundResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Training package not found or unauthorized", response.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-guid")]
    public async Task UpdatePackage_WhenTokenIsInvalidOrMissing_ShouldReturnUnauthorized(string? invalidUserId)
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

        var dto = new UpdateTrainingPackageDto { Title = "Update" };

        // Act
        var result = await controller.UpdatePackage(packageId, dto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        mockService.Verify(s => s.UpdatePackageAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UpdateTrainingPackageDto>()), Times.Never);
    }

    #endregion
}
