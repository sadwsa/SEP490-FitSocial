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
/// Unit tests for View Training Packages List feature (actor: COACH)
/// Covers both Application Layer (TrainingPackageService.GetMyPackagesAsync)
/// and API Presentation Layer (TrainingPackagesController.GetMyPackages).
/// </summary>
public class ViewCoachTrainingPackagesListServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly TrainingPackageService _service;

    public ViewCoachTrainingPackagesListServiceTests()
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

    #region Helper Factories

    private static TrainingPackage CreatePackage(
        Guid coachId,
        Guid? packageId = null,
        string title = "Personal Fitness Coaching",
        string description = "Customized workout and diet plan",
        decimal price = 1500000m,
        int durationDays = 30,
        short? sessionCount = 12,
        short? minAge = 18,
        string? targetAudience = "Adults seeking hypertrophy",
        string packageType = "BOTH",
        bool isActive = true,
        string coachFullName = "Coach Tran Van B",
        List<CoachCertificate>? certificates = null,
        List<TrainingPackageMedium>? media = null)
    {
        var pkgId = packageId ?? Guid.NewGuid();
        var coachUser = new User
        {
            UserId = coachId,
            FullName = coachFullName,
            Email = "coach@fitsocial.com"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = coachUser,
            CoachCertificates = certificates ?? new List<CoachCertificate>()
        };

        return new TrainingPackage
        {
            PackageId = pkgId,
            CoachId = coachId,
            Coach = coachProfile,
            Title = title,
            Description = description,
            Price = price,
            DurationDays = durationDays,
            SessionCount = sessionCount,
            MinAge = minAge,
            TargetAudience = targetAudience,
            PackageType = packageType,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            Media = media ?? new List<TrainingPackageMedium>()
        };
    }

    private static TrainingPackageMedium CreateMedia(
        Guid packageId,
        string url,
        short sortOrder,
        string mediaType = "IMAGE")
    {
        return new TrainingPackageMedium
        {
            MediaId = Guid.NewGuid(),
            PackageId = packageId,
            MediaUrl = url,
            MediaType = mediaType,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static CoachCertificate CreateCertificate(
        Guid coachId,
        string verificationStatus,
        DateOnly? expiryDate)
    {
        return new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CoachId = coachId,
            CertificateName = "Certified Personal Trainer (CPT)",
            VerificationStatus = verificationStatus,
            ExpiryDate = expiryDate,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region 1. Application Layer (Service) Tests

    [Fact]
    public async Task GetMyPackagesAsync_WhenCoachHasActivePackages_ShouldReturnOnlyActivePackages()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var activePkg1 = CreatePackage(coachId, title: "Hypertrophy Phase 1", isActive: true);
        var activePkg2 = CreatePackage(coachId, title: "Fat Loss Essentials", isActive: true);
        var inactivePkg = CreatePackage(coachId, title: "Legacy Deprecated Package", isActive: false);

        var repoPackages = new List<TrainingPackage> { activePkg1, activePkg2, inactivePkg };

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoPackages);

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Title == "Legacy Deprecated Package");
        Assert.Contains(result, p => p.Title == "Hypertrophy Phase 1");
        Assert.Contains(result, p => p.Title == "Fat Loss Essentials");
        Assert.All(result, p => Assert.True(p.IsActive));
    }

    [Fact]
    public async Task GetMyPackagesAsync_WhenCoachHasInactiveOrSoftDeletedPackagesOnly_ShouldReturnEmptyList()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var inactivePkg1 = CreatePackage(coachId, title: "Inactive 1", isActive: false);
        var inactivePkg2 = CreatePackage(coachId, title: "Inactive 2", isActive: false);

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { inactivePkg1, inactivePkg2 });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyPackagesAsync_WhenCoachHasNoPackages_ShouldReturnEmptyList()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage>());

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyPackagesAsync_ShouldMapAllPackagePropertiesCorrectly()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = CreatePackage(
            coachId: coachId,
            packageId: packageId,
            title: "Pro 1-on-1 VIP Bodybuilding",
            description: "High-intensity progressive overload program",
            price: 2500000m,
            durationDays: 60,
            sessionCount: 24,
            minAge: 16,
            targetAudience: "Serious bodybuilding trainees",
            packageType: "BOTH",
            isActive: true,
            coachFullName: "HLV Nguyen Van A");

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { package });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(packageId, dto.PackageId);
        Assert.Equal(coachId, dto.CoachId);
        Assert.Equal("HLV Nguyen Van A", dto.CoachName);
        Assert.Equal("Pro 1-on-1 VIP Bodybuilding", dto.Title);
        Assert.Equal("High-intensity progressive overload program", dto.Description);
        Assert.Equal(2500000m, dto.Price);
        Assert.Equal(60, dto.DurationDays);
        Assert.Equal((short)24, dto.SessionCount);
        Assert.Equal((short)16, dto.MinAge);
        Assert.Equal("Serious bodybuilding trainees", dto.TargetAudience);
        Assert.Equal("BOTH", dto.PackageType);
        Assert.True(dto.IsActive);
        Assert.NotNull(dto.CreatedAt);
    }

    [Fact]
    public async Task GetMyPackagesAsync_WithMultipleMedia_ShouldSortMediaBySortOrderAscending()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        // Deliberately unsorted: SortOrder 3, then 1, then 2
        var mediaList = new List<TrainingPackageMedium>
        {
            CreateMedia(packageId, "https://cdn.fitsocial.com/image3.jpg", sortOrder: 3),
            CreateMedia(packageId, "https://cdn.fitsocial.com/image1.jpg", sortOrder: 1),
            CreateMedia(packageId, "https://cdn.fitsocial.com/image2.jpg", sortOrder: 2)
        };

        var package = CreatePackage(coachId, packageId, media: mediaList);

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { package });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(3, dto.Media.Count);
        Assert.Equal("https://cdn.fitsocial.com/image1.jpg", dto.Media[0].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image2.jpg", dto.Media[1].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image3.jpg", dto.Media[2].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image1.jpg", dto.ThumbnailUrl);
    }

    [Fact]
    public async Task GetMyPackagesAsync_WhenMediaIsEmptyOrNull_ShouldHandleGracefully()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var package = CreatePackage(coachId, media: new List<TrainingPackageMedium>());

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { package });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.NotNull(dto.Media);
        Assert.Empty(dto.Media);
        Assert.Null(dto.ThumbnailUrl);
    }

    [Fact]
    public async Task GetMyPackagesAsync_WhenCoachHasValidAndExpiredCertificates_ShouldEvaluatePoliciesProperly()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)); // UTC+7 Indochina Time

        var certs = new List<CoachCertificate>
        {
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddYears(1)),  // Valid
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddMonths(-1)) // Expired
        };

        var package = CreatePackage(coachId, certificates: certs);

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { package });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.True(dto.CoachHasValidCertificates);
        Assert.True(dto.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetMyPackagesAsync_WhenCoachNavigationIsNull_ShouldFallbackToUnknownCoachNameSafely()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "Orphaned Profile Package",
            Price = 500000m,
            IsActive = true,
            Coach = null! // Navigation property missing or unincluded
        };

        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage> { package });

        // Act
        var result = (await _service.GetMyPackagesAsync(coachId)).ToList();

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal("Unknown Coach", dto.CoachName);
        Assert.False(dto.CoachHasValidCertificates);
        Assert.False(dto.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetMyPackagesAsync_ShouldCallRepositoryWithExactCoachId_EnsuringUserIsolation()
    {
        // Arrange
        var currentCoachId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetPackagesByCoachIdAsync(currentCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage>());

        // Act
        await _service.GetMyPackagesAsync(currentCoachId);

        // Assert: Ensure repository was queried with the exact caller's CoachId
        _mockRepo.Verify(r => r.GetPackagesByCoachIdAsync(currentCoachId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.VerifyNoOtherCalls();
    }

    #endregion

    #region 2. API Presentation Layer (Controller) Tests

    [Fact]
    public void GetMyPackages_Endpoint_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        // Arrange & Act
        var methodInfo = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.GetMyPackages));
        Assert.NotNull(methodInfo);

        var authorizeAttr = methodInfo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(authorizeAttr);
        Assert.Equal("COACH", authorizeAttr.Roles);
    }

    [Fact]
    public async Task GetMyPackages_WhenCoachIsAuthenticated_ShouldReturnOkWithPackages()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        // Set up ClaimsPrincipal simulating authenticated COACH
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expectedPackages = new List<TrainingPackageResponseDto>
        {
            new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Workout Plan A", Price = 1000000m, IsActive = true },
            new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Workout Plan B", Price = 1500000m, IsActive = true }
        };

        mockService.Setup(s => s.GetMyPackagesAsync(coachId))
            .ReturnsAsync(expectedPackages);

        // Act
        var actionResult = await controller.GetMyPackages();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Count());
        mockService.Verify(s => s.GetMyPackagesAsync(coachId), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task GetMyPackages_WhenTokenHasInvalidOrMissingUserId_ShouldReturnUnauthorized(string? invalidUserId)
    {
        // Arrange
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

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
        var actionResult = await controller.GetMyPackages();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Invalid token", response.Message);
        mockService.Verify(s => s.GetMyPackagesAsync(It.IsAny<Guid>()), Times.Never);
    }

    #endregion
}

