using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for View Training Packages Details feature (actor: COACH)
/// Covers:
/// 1. Application Layer (TrainingPackageService):
///    - GetPackageByIdAsync: Fetching full package details, inactive draft visibility for coach,
///      media ordering, certificate policies (valid vs expired), fallback unknown coach, isolation.
///    - GetPackageReviewsAsync: Retrieving review summaries, average rating calculation, star breakdown.
///    - ReplyToReviewAsync: Coach replying to trainee reviews, coach ownership isolation check,
///      not found handling.
/// 2. API Presentation Layer (TrainingPackagesController):
///    - GetPackageById: 200 OK, 404 NotFound, AllowAnonymous attribute.
///    - GetPackageReviews: 200 OK review retrieval.
///    - ReplyToReview: Authorize COACH attribute, authenticated coach response, 401 Unauthorized for invalid token.
/// </summary>
public class ViewCoachTrainingPackageDetailsTests
{
    private readonly Mock<ITrainingPackageRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly TrainingPackageService _service;

    public ViewCoachTrainingPackageDetailsTests()
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
        string title = "VIP 1-on-1 Transformation Program",
        string description = "Personalized coaching, customized workout plans and diet guidance",
        decimal price = 2500000m,
        int durationDays = 60,
        short? sessionCount = 24,
        short? minAge = 18,
        string? targetAudience = "Adults looking for body transformation",
        string packageType = "BOTH",
        bool isActive = true,
        string coachFullName = "Coach Nguyen Van A",
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
            CreatedAt = DateTime.UtcNow.AddDays(-15),
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

    private static Review CreateReview(
        Guid packageId,
        Guid coachId,
        Guid traineeId,
        int rating,
        string comment,
        string? reply = null,
        string traineeFullName = "Pham Trainee",
        string? traineeAvatar = "https://cdn.fitsocial.com/trainee.jpg",
        bool isEdited = false)
    {
        return new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = rating,
            Comment = comment,
            Reply = reply,
            IsEdited = isEdited,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = reply != null ? DateTime.UtcNow.AddDays(-1) : null,
            Trainee = new User
            {
                UserId = traineeId,
                FullName = traineeFullName,
                AvatarUrl = traineeAvatar
            },
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User
                {
                    UserId = coachId,
                    FullName = "Coach Nguyen Van A",
                    AvatarUrl = "https://cdn.fitsocial.com/coach.jpg"
                }
            }
        };
    }

    #endregion

    #region 1. Application Layer (Service: GetPackageByIdAsync) Tests

    [Fact]
    public async Task GetPackageByIdAsync_WhenPackageExistsAndOwnedByCoach_ShouldReturnFullDetailsAccurately()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = CreatePackage(
            coachId: coachId,
            packageId: packageId,
            title: "VIP 1-on-1 Transformation Program",
            description: "Personalized coaching, customized workout plans and diet guidance",
            price: 2500000m,
            durationDays: 60,
            sessionCount: 24,
            minAge: 18,
            targetAudience: "Adults looking for body transformation",
            packageType: "BOTH",
            isActive: true,
            coachFullName: "HLV Nguyen Van A");

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.Equal(coachId, result.CoachId);
        Assert.Equal("HLV Nguyen Van A", result.CoachName);
        Assert.Equal("VIP 1-on-1 Transformation Program", result.Title);
        Assert.Equal("Personalized coaching, customized workout plans and diet guidance", result.Description);
        Assert.Equal(2500000m, result.Price);
        Assert.Equal(60, result.DurationDays);
        Assert.Equal((short)24, result.SessionCount);
        Assert.Equal((short)18, result.MinAge);
        Assert.Equal("Adults looking for body transformation", result.TargetAudience);
        Assert.Equal("BOTH", result.PackageType);
        Assert.True(result.IsActive);
        Assert.NotNull(result.CreatedAt);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenPackageIsInactiveOrDraft_CoachCanStillViewDetails()
    {
        // Arrange
        // Coaches must be able to view their own draft or disabled packages to manage/edit them
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var inactivePackage = CreatePackage(
            coachId: coachId,
            packageId: packageId,
            title: "Draft Weight Loss Program",
            isActive: false);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactivePackage);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(packageId, result.PackageId);
        Assert.False(result.IsActive);
        Assert.Equal("Draft Weight Loss Program", result.Title);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenPackageDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act
        var result = await _service.GetPackageByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
        _mockRepo.Verify(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachNavigationIsNull_ShouldFallbackToUnknownCoachSafely()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = Guid.NewGuid(),
            Title = "Package Without Coach Navigation",
            Price = 1000000m,
            IsActive = true,
            Coach = null! // Navigation missing
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unknown Coach", result.CoachName);
        Assert.False(result.CoachHasValidCertificates);
        Assert.False(result.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenUserNavigationInsideCoachProfileIsNull_ShouldFallbackToUnknownCoachSafely()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Package Without User In Coach Profile",
            Price = 1000000m,
            IsActive = true,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = null! // User navigation is null
            }
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unknown Coach", result.CoachName);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WithMultipleMedia_ShouldSortMediaBySortOrderAscending()
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

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Media.Count);
        Assert.Equal("https://cdn.fitsocial.com/image1.jpg", result.Media[0].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image2.jpg", result.Media[1].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image3.jpg", result.Media[2].MediaUrl);
        Assert.Equal("https://cdn.fitsocial.com/image1.jpg", result.ThumbnailUrl);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenMediaIsEmptyOrNull_ShouldReturnEmptyMediaListAndNullThumbnail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = CreatePackage(coachId, packageId, media: new List<TrainingPackageMedium>());

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Media);
        Assert.Empty(result.Media);
        Assert.Null(result.ThumbnailUrl);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachHasValidAndExpiredCertificates_ShouldEvaluatePoliciesProperly()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var today = CoachCertificatePolicy.GetBusinessToday();

        var certs = new List<CoachCertificate>
        {
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddYears(1)),  // Valid
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddMonths(-1)) // Expired
        };

        var package = CreatePackage(coachId, packageId, certificates: certs);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CoachHasValidCertificates);
        Assert.True(result.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachHasOnlyValidCertificates_ShouldFlagValidTrueAndExpiredFalse()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var today = CoachCertificatePolicy.GetBusinessToday();

        var certs = new List<CoachCertificate>
        {
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddYears(2))
        };

        var package = CreatePackage(coachId, packageId, certificates: certs);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CoachHasValidCertificates);
        Assert.False(result.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachHasOnlyExpiredCertificates_ShouldFlagValidFalseAndExpiredTrue()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var today = CoachCertificatePolicy.GetBusinessToday();

        var certs = new List<CoachCertificate>
        {
            CreateCertificate(coachId, verificationStatus: "APPROVED", expiryDate: today.AddDays(-10))
        };

        var package = CreatePackage(coachId, packageId, certificates: certs);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CoachHasValidCertificates);
        Assert.True(result.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachHasPendingOrRejectedCertificatesOnly_ShouldFlagValidFalse()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var today = CoachCertificatePolicy.GetBusinessToday();

        var certs = new List<CoachCertificate>
        {
            CreateCertificate(coachId, verificationStatus: "PENDING", expiryDate: today.AddYears(1)),
            CreateCertificate(coachId, verificationStatus: "REJECTED", expiryDate: today.AddYears(1))
        };

        var package = CreatePackage(coachId, packageId, certificates: certs);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CoachHasValidCertificates);
        Assert.False(result.CoachHasExpiredCertificates);
    }

    [Fact]
    public async Task GetPackageByIdAsync_WhenCoachHasNoCertificates_ShouldFlagBothPoliciesFalse()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = CreatePackage(coachId, packageId, certificates: new List<CoachCertificate>());

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.CoachHasValidCertificates);
        Assert.False(result.CoachHasExpiredCertificates);
    }

    [Theory]
    [InlineData("WORKOUT", "WORKOUT")]
    [InlineData("MEAL", "MEAL")]
    [InlineData("BOTH", "BOTH")]
    [InlineData(null, "BOTH")] // Defaults to BOTH when null
    public async Task GetPackageByIdAsync_WithDifferentPackageTypes_ShouldMapPackageTypeCorrectly(string? inputType, string expectedType)
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var package = CreatePackage(coachId, packageId, packageType: inputType!);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        var result = await _service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedType, result.PackageType);
    }

    [Fact]
    public async Task GetPackageByIdAsync_ShouldCallRepositoryWithExactPackageId()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var package = CreatePackage(coachId, packageId);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // Act
        await _service.GetPackageByIdAsync(packageId);

        // Assert
        _mockRepo.Verify(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.VerifyNoOtherCalls();
    }

    #endregion

    #region 2. Application Layer (Service: GetPackageReviewsAsync) Tests

    [Fact]
    public async Task GetPackageReviewsAsync_WhenPackageHasMultipleReviews_ShouldCalculateSummaryCorrectly()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        // 5 reviews: ratings 5, 5, 4, 3, 1 -> Sum = 18 -> Avg = 18/5 = 3.6
        var reviews = new List<Review>
        {
            CreateReview(packageId, coachId, Guid.NewGuid(), 5, "Great program! Highly recommend.", reply: "Thanks for training hard!"),
            CreateReview(packageId, coachId, Guid.NewGuid(), 5, "Amazing coach!", isEdited: true),
            CreateReview(packageId, coachId, Guid.NewGuid(), 4, "Good workout routine."),
            CreateReview(packageId, coachId, Guid.NewGuid(), 3, "Average, needs more meal variety."),
            CreateReview(packageId, coachId, Guid.NewGuid(), 1, "Not satisfied with response time.")
        };

        _mockReviewRepo.Setup(r => r.GetReviewsByPackageIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);

        // Act
        var response = await _service.GetPackageReviewsAsync(packageId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var summary = response.Data;
        Assert.Equal(5, summary.TotalReviews);
        Assert.Equal(3.6, summary.AverageRating);

        // Verify Star Breakdown
        Assert.Equal(2, summary.RatingCounts[5]);
        Assert.Equal(1, summary.RatingCounts[4]);
        Assert.Equal(1, summary.RatingCounts[3]);
        Assert.Equal(0, summary.RatingCounts[2]);
        Assert.Equal(1, summary.RatingCounts[1]);

        // Verify Review Mapping
        Assert.Equal(5, summary.Reviews.Count);
        var firstReview = summary.Reviews[0];
        Assert.Equal("Great program! Highly recommend.", firstReview.Comment);
        Assert.Equal("Thanks for training hard!", firstReview.Reply);
        Assert.Equal(5, firstReview.Rating);
        Assert.Equal("Coach Nguyen Van A", firstReview.CoachName);
        Assert.Equal("https://cdn.fitsocial.com/coach.jpg", firstReview.CoachAvatarUrl);
        Assert.Equal("Pham Trainee", firstReview.TraineeName);
        Assert.False(firstReview.IsEdited);

        var secondReview = summary.Reviews[1];
        Assert.True(secondReview.IsEdited);
    }

    [Fact]
    public async Task GetPackageReviewsAsync_WhenPackageHasNoReviews_ShouldReturnEmptySummaryWithDefaultCounts()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        _mockReviewRepo.Setup(r => r.GetReviewsByPackageIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review>());

        // Act
        var response = await _service.GetPackageReviewsAsync(packageId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var summary = response.Data;
        Assert.Equal(0, summary.TotalReviews);
        Assert.Equal(0.0, summary.AverageRating);
        Assert.Empty(summary.Reviews);
        Assert.Equal(0, summary.RatingCounts[5]);
        Assert.Equal(0, summary.RatingCounts[4]);
        Assert.Equal(0, summary.RatingCounts[3]);
        Assert.Equal(0, summary.RatingCounts[2]);
        Assert.Equal(0, summary.RatingCounts[1]);
    }

    [Fact]
    public async Task GetPackageReviewsAsync_WhenSomeReviewsHaveNullRatings_ShouldFilterAndCalculateAverageBasedOnValidRatings()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var reviews = new List<Review>
        {
            new Review
            {
                ReviewId = Guid.NewGuid(),
                PackageId = packageId,
                CoachId = coachId,
                TraineeId = Guid.NewGuid(),
                Rating = null, // Null rating
                Comment = "No rating given",
                Trainee = new User { FullName = "User 1" }
            },
            new Review
            {
                ReviewId = Guid.NewGuid(),
                PackageId = packageId,
                CoachId = coachId,
                TraineeId = Guid.NewGuid(),
                Rating = 4,
                Comment = "Solid plan",
                Trainee = new User { FullName = "User 2" }
            }
        };

        _mockReviewRepo.Setup(r => r.GetReviewsByPackageIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviews);

        // Act
        var response = await _service.GetPackageReviewsAsync(packageId);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var summary = response.Data;
        Assert.Equal(1, summary.TotalReviews); // Only 1 valid rating
        Assert.Equal(4.0, summary.AverageRating);
        Assert.Equal(1, summary.RatingCounts[4]);
    }

    #endregion

    #region 3. Application Layer (Service: ReplyToReviewAsync - Coach Action on Details Page) Tests

    [Fact]
    public async Task ReplyToReviewAsync_WhenCoachOwnsPackage_ShouldSuccessfullySaveReply()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        var package = CreatePackage(coachId: coachId, packageId: packageId);
        var review = new Review
        {
            ReviewId = reviewId,
            PackageId = packageId,
            CoachId = coachId,
            TraineeId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Excellent workouts!",
            Reply = null
        };

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var replyDto = new ReplyReviewDto
        {
            Reply = "   Thank you for your feedback! Keep pushing hard!   "
        };

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, replyDto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Reply posted successfully.", result.Message);
        Assert.Equal("Thank you for your feedback! Keep pushing hard!", review.Reply); // Trimmed
        Assert.NotNull(review.UpdatedAt);

        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReplyToReviewAsync_WhenCoachDoesNotOwnPackage_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        // Security check: Coach B tries to reply to a review on Coach A's package
        var coachAId = Guid.NewGuid();
        var coachBId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        var packageOfCoachA = CreatePackage(coachId: coachAId, packageId: packageId);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageOfCoachA);

        var replyDto = new ReplyReviewDto { Reply = "Attempting unauthorized reply" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachBId, replyDto));

        Assert.Equal("Only the coach who owns this package can reply to its reviews.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplyToReviewAsync_WhenPackageNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        var replyDto = new ReplyReviewDto { Reply = "Hello" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, replyDto));

        Assert.Equal("Package not found.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplyToReviewAsync_WhenReviewNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        var package = CreatePackage(coachId: coachId, packageId: packageId);

        _mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        var replyDto = new ReplyReviewDto { Reply = "Hello" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, replyDto));

        Assert.Equal("Review not found for this package.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReplyToReviewAsync_WhenReviewBelongsToDifferentPackage_ShouldThrowNotFoundException()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var targetPackageId = Guid.NewGuid();
        var anotherPackageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();

        var targetPackage = CreatePackage(coachId: coachId, packageId: targetPackageId);
        var reviewBelongingToAnotherPackage = new Review
        {
            ReviewId = reviewId,
            PackageId = anotherPackageId, // Mismatched package ID
            CoachId = coachId,
            TraineeId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Review for a different package"
        };

        _mockRepo.Setup(r => r.GetByIdAsync(targetPackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetPackage);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewBelongingToAnotherPackage);

        var replyDto = new ReplyReviewDto { Reply = "Hello" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(targetPackageId, reviewId, coachId, replyDto));

        Assert.Equal("Review not found for this package.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 4. API Presentation Layer (TrainingPackagesController) Tests

    [Fact]
    public void GetPackageById_Endpoint_AllowsAnonymous_EnsuringCoachAndGuestsCanAccess()
    {
        // Arrange & Act
        var methodInfo = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.GetPackageById));
        Assert.NotNull(methodInfo);

        var allowAnonymousAttr = methodInfo.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Cast<AllowAnonymousAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(allowAnonymousAttr);
    }

    [Fact]
    public async Task GetPackageById_WhenPackageExists_ShouldReturnOkWithDetails()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        // Coach user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var expectedDto = new TrainingPackageResponseDto
        {
            PackageId = packageId,
            CoachId = coachId,
            CoachName = "Coach Nguyen Van A",
            Title = "Elite Body Transformation",
            Price = 2000000m,
            DurationDays = 60,
            SessionCount = 24,
            MinAge = 18,
            PackageType = "BOTH",
            IsActive = true
        };

        mockService.Setup(s => s.GetPackageByIdAsync(packageId))
            .ReturnsAsync(expectedDto);

        // Act
        var actionResult = await controller.GetPackageById(packageId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(packageId, response.Data.PackageId);
        Assert.Equal(coachId, response.Data.CoachId);
        Assert.Equal("Elite Body Transformation", response.Data.Title);
        mockService.Verify(s => s.GetPackageByIdAsync(packageId), Times.Once);
    }

    [Fact]
    public async Task GetPackageById_WhenPackageNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        mockService.Setup(s => s.GetPackageByIdAsync(packageId))
            .ReturnsAsync((TrainingPackageResponseDto?)null);

        // Act
        var actionResult = await controller.GetPackageById(packageId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<TrainingPackageResponseDto>>(notFoundResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Training package not found", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task GetPackageReviews_WhenCalled_ShouldReturnOkWithSummary()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        var expectedSummary = new PackageReviewSummaryDto
        {
            TotalReviews = 3,
            AverageRating = 4.7
        };

        mockService.Setup(s => s.GetPackageReviewsAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PackageReviewSummaryDto>.Ok(expectedSummary));

        // Act
        var actionResult = await controller.GetPackageReviews(packageId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<PackageReviewSummaryDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(3, response.Data.TotalReviews);
        Assert.Equal(4.7, response.Data.AverageRating);
    }

    [Fact]
    public void ReplyToReview_Endpoint_HasAuthorizeAttribute_WithCoachRoleOnly()
    {
        // Arrange & Act
        var methodInfo = typeof(TrainingPackagesController).GetMethod(nameof(TrainingPackagesController.ReplyToReview));
        Assert.NotNull(methodInfo);

        var authorizeAttr = methodInfo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        // Assert
        Assert.NotNull(authorizeAttr);
        Assert.Equal("COACH", authorizeAttr.Roles);
    }

    [Fact]
    public async Task ReplyToReview_WhenCoachIsAuthenticated_ShouldReturnOkWithSuccess()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var mockService = new Mock<ITrainingPackageService>();
        var controller = new TrainingPackagesController(mockService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, coachId.ToString()),
            new Claim(ClaimTypes.Role, "COACH")
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var replyDto = new ReplyReviewDto { Reply = "Thank you so much!" };

        mockService.Setup(s => s.ReplyToReviewAsync(packageId, reviewId, coachId, replyDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<bool>.Ok(true, "Reply posted successfully."));

        // Act
        var actionResult = await controller.ReplyToReview(packageId, reviewId, replyDto, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<bool>>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.Data);
        Assert.Equal("Reply posted successfully.", response.Message);
        mockService.Verify(s => s.ReplyToReviewAsync(packageId, reviewId, coachId, replyDto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-guid")]
    public async Task ReplyToReview_WhenTokenHasInvalidOrMissingUserId_ShouldReturnUnauthorized(string? invalidUserId)
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
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

        var replyDto = new ReplyReviewDto { Reply = "Thank you!" };

        // Act
        var actionResult = await controller.ReplyToReview(packageId, reviewId, replyDto, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponseDto<bool>>(unauthorizedResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Invalid token", response.Message);
        mockService.Verify(s => s.ReplyToReviewAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ReplyReviewDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

