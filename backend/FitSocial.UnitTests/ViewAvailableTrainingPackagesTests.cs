using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
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
/// Unit tests for the "View Available Training Package" feature:
/// Viewing available training packages on a Coach's public profile and via training package search by coach.
/// Covers all 5 test categories:
/// 1. Happy Path: Active packages displayed on coach profile, field mapping, filtered packages by coachId, 200 OK.
/// 2. Negative Case: Coach profile not found throws NotFoundException, non-existent coach packages returns empty.
/// 3. Boundary Case: Coach has 0 packages, all packages are inactive, fallback to default name, combined filters.
/// 4. Exception Case: Database connection failures, repository exception propagation, controller exception handling.
/// 5. Authorization Case: [AllowAnonymous] verification on both endpoints, guest & trainee access without token.
/// </summary>
public class ViewAvailableTrainingPackagesTests
{
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<ILocationRepository> _mockLocationRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ITrainingPackageRepository> _mockPackageRepo;
    private readonly CoachService _coachService;
    private readonly TrainingPackageService _packageService;

    public ViewAvailableTrainingPackagesTests()
    {
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockLocationRepo = new Mock<ILocationRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockPackageRepo = new Mock<ITrainingPackageRepository>();

        _coachService = new CoachService(
            _mockCoachRepo.Object,
            _mockLocationRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object);

        _packageService = new TrainingPackageService(
            _mockPackageRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object);
    }

    #region 1. Happy Path Cases

    [Fact]
    public async Task GetCoachDetailsAsync_WhenCoachHasActiveAndInactivePackages_ShouldReturnOnlyActivePackages()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Master Coach Tony",
            Email = "tony@fitsocial.com"
        };

        var package1 = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "30-Day Shred",
            Price = 1200000m,
            DurationDays = 30,
            SessionCount = (short)12,
            IsActive = true
        };

        var package2 = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "60-Day Muscle Gain",
            Price = 2500000m,
            DurationDays = 60,
            SessionCount = (short)24,
            IsActive = true
        };

        var inactivePackage = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "Old Deprecated Plan",
            Price = 800000m,
            DurationDays = 14,
            IsActive = false // Should be filtered out
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = coachUser,
            Bio = "Certified personal trainer with 8 years of experience.",
            ExperienceYears = 8,
            TrainingPackages = new List<TrainingPackage> { package1, package2, inactivePackage },
            CoachCertificates = new List<CoachCertificate>(),
            Locations = new List<Location>(),
            Reviews = new List<Review>()
        };

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        // Act
        var response = await _coachService.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Packages.Count);
        Assert.All(response.Data.Packages, p => Assert.True(p.IsActive));
        Assert.Contains(response.Data.Packages, p => p.Title == "30-Day Shred");
        Assert.Contains(response.Data.Packages, p => p.Title == "60-Day Muscle Gain");
        Assert.DoesNotContain(response.Data.Packages, p => p.Title == "Old Deprecated Plan");
    }

    [Fact]
    public async Task GetCoachDetailsAsync_MapsAllAvailablePackageFieldsAccurately()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var createdDate = DateTime.UtcNow.AddDays(-10);

        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Coach Sarah Connor"
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Functional Crossfit Pro",
            Description = "High intensity functional strength workouts",
            Price = 1800000m,
            DurationDays = 45,
            SessionCount = (short)15,
            MinAge = (short)18,
            TargetAudience = "Intermediate to Advanced athletes",
            IsActive = true,
            CreatedAt = createdDate
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = coachUser,
            TrainingPackages = new List<TrainingPackage> { package },
            CoachCertificates = new List<CoachCertificate>(),
            Locations = new List<Location>(),
            Reviews = new List<Review>()
        };

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        // Act
        var response = await _coachService.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(response.Data);
        var pkgDto = Assert.Single(response.Data.Packages);
        Assert.Equal(packageId, pkgDto.PackageId);
        Assert.Equal(coachId, pkgDto.CoachId);
        Assert.Equal("Coach Sarah Connor", pkgDto.CoachName);
        Assert.Equal("Functional Crossfit Pro", pkgDto.Title);
        Assert.Equal("High intensity functional strength workouts", pkgDto.Description);
        Assert.Equal(1800000m, pkgDto.Price);
        Assert.Equal(45, pkgDto.DurationDays);
        Assert.Equal((short)15, pkgDto.SessionCount);
        Assert.Equal((short)18, pkgDto.MinAge);
        Assert.Equal("Intermediate to Advanced athletes", pkgDto.TargetAudience);
        Assert.True(pkgDto.IsActive);
        Assert.Equal(createdDate, pkgDto.CreatedAt);
    }

    [Fact]
    public async Task GetAllPackagesAsync_WithCoachId_ReturnsActivePackagesOfSpecificCoach()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var activePackages = new List<TrainingPackage>
        {
            new()
            {
                PackageId = Guid.NewGuid(),
                CoachId = coachId,
                Title = "Pilates Core & Posture",
                Price = 1000000m,
                IsActive = true
            }
        };

        _mockPackageRepo.Setup(r => r.GetAllActivePackagesAsync(null, null, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activePackages);

        // Act
        var result = (await _packageService.GetAllPackagesAsync(null, null, coachId)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("Pilates Core & Posture", result[0].Title);
        Assert.Equal(coachId, result[0].CoachId);
        _mockPackageRepo.Verify(r => r.GetAllActivePackagesAsync(null, null, coachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoachDetails_Controller_WhenCoachExists_ReturnsOkWithPackages()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockCoachService = new Mock<ICoachService>();
        var mockBankAccountService = new Mock<ICoachBankAccountService>();
        var controller = new CoachesController(mockCoachService.Object, mockBankAccountService.Object);

        var coachDetail = new CoachDetailDto
        {
            CoachId = coachId,
            FullName = "Coach Mike",
            Packages = new List<TrainingPackageResponseDto>
            {
                new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Cardio Blast", Price = 900000m }
            }
        };

        mockCoachService.Setup(s => s.GetCoachDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDetailDto>.Ok(coachDetail));

        // Act
        var result = await controller.GetCoachDetails(coachId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<CoachDetailDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data.Packages);
        Assert.Equal("Cardio Blast", response.Data.Packages[0].Title);
    }

    [Fact]
    public async Task GetAllPackages_Controller_WithCoachId_ReturnsOkWithPackages()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        var packages = new List<TrainingPackageResponseDto>
        {
            new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Strength 101" }
        };

        mockService.Setup(s => s.GetAllPackagesAsync(null, null, coachId))
            .ReturnsAsync(packages);

        // Act
        var result = await controller.GetAllPackages(null, null, coachId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    #endregion

    #region 2. Negative Cases

    [Fact]
    public async Task GetCoachDetailsAsync_WhenCoachNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentCoachId = Guid.NewGuid();

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(nonExistentCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachProfile?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _coachService.GetCoachDetailsAsync(nonExistentCoachId));

        Assert.Equal("Coach not found.", ex.Message);
    }

    [Fact]
    public async Task GetAllPackagesAsync_WhenCoachHasNoMatchingPackages_ReturnsEmptyCollection()
    {
        // Arrange
        var coachId = Guid.NewGuid();

        _mockPackageRepo.Setup(r => r.GetAllActivePackagesAsync(null, null, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage>());

        // Act
        var result = await _packageService.GetAllPackagesAsync(null, null, coachId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region 3. Boundary Cases

    [Fact]
    public async Task GetCoachDetailsAsync_WhenCoachHasZeroPackages_ReturnsEmptyPackagesList()
    {
        // Arrange (New coach profile with 0 packages created yet)
        var coachId = Guid.NewGuid();
        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = new User { UserId = coachId, FullName = "New Coach" },
            TrainingPackages = new List<TrainingPackage>(), // 0 packages
            CoachCertificates = new List<CoachCertificate>(),
            Locations = new List<Location>(),
            Reviews = new List<Review>()
        };

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        // Act
        var response = await _coachService.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Data.Packages);
        Assert.Empty(response.Data.Packages);
    }

    [Fact]
    public async Task GetCoachDetailsAsync_WhenAllPackagesAreInactive_ReturnsEmptyPackagesList()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = new User { UserId = coachId, FullName = "Coach On Break" },
            TrainingPackages = new List<TrainingPackage>
            {
                new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Draft 1", IsActive = false },
                new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Draft 2", IsActive = false }
            },
            CoachCertificates = new List<CoachCertificate>(),
            Locations = new List<Location>(),
            Reviews = new List<Review>()
        };

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        // Act
        var response = await _coachService.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data.Packages);
    }

    [Fact]
    public async Task GetCoachDetailsAsync_WhenCoachNavigationIsNull_FallbackToDefaultName()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = "Independent Package",
            IsActive = true
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Coach = null!, // Navigation is null
            TrainingPackages = new List<TrainingPackage> { package },
            CoachCertificates = new List<CoachCertificate>(),
            Locations = new List<Location>(),
            Reviews = new List<Review>()
        };

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        // Act
        var response = await _coachService.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(response.Data);
        var pkg = Assert.Single(response.Data.Packages);
        Assert.Equal("Coach", pkg.CoachName); // Fallback applied
    }

    [Fact]
    public async Task GetAllPackagesAsync_WithSearchKeywordPriceAndCoachId_PassesAllFiltersToRepository()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var keyword = "Strength";
        var maxPrice = 3000000m;

        _mockPackageRepo.Setup(r => r.GetAllActivePackagesAsync(keyword, maxPrice, coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TrainingPackage>());

        // Act
        var result = await _packageService.GetAllPackagesAsync(keyword, maxPrice, coachId);

        // Assert
        Assert.NotNull(result);
        _mockPackageRepo.Verify(r => r.GetAllActivePackagesAsync(keyword, maxPrice, coachId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 4. Exception Cases

    [Fact]
    public async Task GetCoachDetailsAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var coachId = Guid.NewGuid();

        _mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection timeout."));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _coachService.GetCoachDetailsAsync(coachId));

        Assert.Equal("Database connection timeout.", ex.Message);
    }

    [Fact]
    public async Task GetAllPackagesAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var coachId = Guid.NewGuid();

        _mockPackageRepo.Setup(r => r.GetAllActivePackagesAsync(null, null, coachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to query database"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _packageService.GetAllPackagesAsync(null, null, coachId));

        Assert.Equal("Failed to query database", ex.Message);
    }

    #endregion

    #region 5. Authorization Cases

    [Fact]
    public void GetCoachDetails_Endpoint_HasAllowAnonymousAttribute()
    {
        var method = typeof(CoachesController).GetMethod(nameof(CoachesController.GetCoachDetails));
        Assert.NotNull(method);

        var allowAnonymousAttr = method.GetCustomAttributes(typeof(AllowAnonymousAttribute), false)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        Assert.NotNull(allowAnonymousAttr);
    }

    [Fact]
    public void GetAllPackages_Endpoint_HasAllowAnonymousAttribute()
    {
        var method = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.GetAllPackages));
        Assert.NotNull(method);

        var allowAnonymousAttr = method.GetCustomAttributes(typeof(AllowAnonymousAttribute), false)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        Assert.NotNull(allowAnonymousAttr);
    }

    [Fact]
    public async Task GetCoachDetails_CanBeAccessedByUnauthenticatedGuest_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockCoachService = new Mock<ICoachService>();
        var mockBankAccountService = new Mock<ICoachBankAccountService>();
        var controller = new CoachesController(mockCoachService.Object, mockBankAccountService.Object);

        // Guest user: Unauthenticated identity
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var expectedDetail = new CoachDetailDto
        {
            CoachId = coachId,
            FullName = "Public Coach",
            Packages = new List<TrainingPackageResponseDto>
            {
                new() { PackageId = Guid.NewGuid(), Title = "Beginner Yoga", Price = 500000m }
            }
        };

        mockCoachService.Setup(s => s.GetCoachDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<CoachDetailDto>.Ok(expectedDetail));

        // Act
        var result = await controller.GetCoachDetails(coachId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<CoachDetailDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Packages);
    }

    [Fact]
    public async Task GetAllPackages_WithCoachId_CanBeAccessedByUnauthenticatedGuest_ReturnsOk()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        mockService.Setup(s => s.GetAllPackagesAsync(null, null, coachId))
            .ReturnsAsync(new List<TrainingPackageResponseDto>
            {
                new() { PackageId = Guid.NewGuid(), CoachId = coachId, Title = "Crossfit Standard" }
            });

        // Act
        var result = await controller.GetAllPackages(null, null, coachId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    #endregion
}

