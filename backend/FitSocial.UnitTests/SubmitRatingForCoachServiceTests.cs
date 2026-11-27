using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for Submit Rating for Coach (TrainingPackageService.SubmitPackageReviewAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID20).
/// </summary>
public class SubmitRatingForCoachServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _mockPackageRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<IReviewMediaRepository> _mockReviewMediaRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly TrainingPackageService _service;

    public SubmitRatingForCoachServiceTests()
    {
        _mockPackageRepo = new Mock<ITrainingPackageRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockReviewMediaRepo = new Mock<IReviewMediaRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockEmailService = new Mock<IEmailService>();

        _service = new TrainingPackageService(
            _mockPackageRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object,
            _mockReviewMediaRepo.Object,
            _mockUserRepo.Object,
            _mockNotificationRepo.Object,
            _mockEmailService.Object);
    }

    private static TrainingPackage CreateEligiblePackage(
        Guid packageId,
        Guid coachId,
        string? approvalStatus = CoachConstants.ApprovalStatusApproved,
        string? status = CoachConstants.StatusActive)
    {
        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Full Body Transformation",
            Price = 1500000m,
            IsActive = true,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                ApprovalStatus = approvalStatus,
                Status = status
            }
        };
    }

    private static Order CreateEligibleOrder(
        Guid orderId,
        Guid traineeId,
        Guid packageId,
        string? orderStatus = PaymentConstants.OrderStatusPaid)
    {
        return new Order
        {
            OrderId = orderId,
            BuyerId = traineeId,
            OrderStatus = orderStatus,
            OrderDetails = new List<OrderDetail>
            {
                new()
                {
                    OrderDetailsId = Guid.NewGuid(),
                    OrderId = orderId,
                    PackageId = packageId
                }
            }
        };
    }

    #region Normal Cases (N)

    [Fact(DisplayName = "UTCID01 - Submit new 5-star review with comment and 2 images on PAID order succeeds")]
    public async Task UTCID01_SubmitPackageReviewAsync_NewReviewWithImages_ReturnsOkAndPersistsMediaAndNotification()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Excellent coach and program!",
            ImageUrls = new List<string> { "https://cdn.example.com/r1.jpg", "https://cdn.example.com/r2.jpg" }
        };

        var savedReview = new Review { ReviewId = Guid.NewGuid(), OrderId = orderId, PackageId = packageId, TraineeId = traineeId, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.SetupSequence(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null)
            .ReturnsAsync(savedReview);
        _mockReviewMediaRepo.Setup(r => r.GetMediaByReviewIdAsync(savedReview.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewMedia>());
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = coachId, FullName = "Coach Alex", Email = "coach@example.com" });
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = traineeId, FullName = "Trainee Sam" });

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Review submitted successfully.", result.Message);
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev =>
            rev.PackageId == packageId &&
            rev.OrderId == orderId &&
            rev.CoachId == coachId &&
            rev.TraineeId == traineeId &&
            rev.Rating == 5 &&
            rev.Comment == "Excellent coach and program!" &&
            rev.IsEdited == false), It.IsAny<CancellationToken>()), Times.Once);
        _mockReviewMediaRepo.Verify(r => r.AddAsync(It.IsAny<ReviewMedia>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockNotificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.Type == "NEW_REVIEW" && n.UserId == coachId), It.IsAny<CancellationToken>()), Times.Once);
        _mockEmailService.Verify(e => e.SendEmailAsync("coach@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 - Submit new 4-star review on COMPLETED order with empty images succeeds")]
    public async Task UTCID02_SubmitPackageReviewAsync_CompletedOrderWithEmptyImages_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusCompleted);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 4,
            Comment = "Good workout plan",
            ImageUrls = new List<string>()
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Review submitted successfully.", result.Message);
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev => rev.Rating == 4 && rev.IsEdited == false), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 - Submit new review on ACTIVE order with null comment and null ImageUrls succeeds")]
    public async Task UTCID03_SubmitPackageReviewAsync_ActiveOrderWithNullCommentAndNullImages_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusActive);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = null,
            ImageUrls = null!
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        _mockReviewMediaRepo.Verify(r => r.GetMediaByReviewIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 - Edit existing unedited review updates review and replaces existing media")]
    public async Task UTCID04_SubmitPackageReviewAsync_ExistingUneditedReview_UpdatesReviewAndReplacesMedia()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var existingReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = 3,
            Comment = "Initial comment",
            IsEdited = false
        };
        var oldMedia = new ReviewMedia { ReviewMediaId = Guid.NewGuid(), ReviewId = existingReview.ReviewId, MediaUrl = "https://cdn.example.com/old.jpg" };

        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Updated feedback after finishing sessions",
            ImageUrls = new List<string> { "https://cdn.example.com/new.jpg" }
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReview);
        _mockReviewMediaRepo.Setup(r => r.GetMediaByReviewIdAsync(existingReview.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewMedia> { oldMedia });
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = coachId, FullName = "Coach Alex", Email = "coach@example.com" });

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, existingReview.Rating);
        Assert.Equal("Updated feedback after finishing sessions", existingReview.Comment);
        Assert.True(existingReview.IsEdited);
        Assert.NotNull(existingReview.UpdatedAt);
        _mockReviewRepo.Verify(r => r.Update(existingReview), Times.Once);
        _mockReviewMediaRepo.Verify(r => r.Remove(oldMedia), Times.Once);
        _mockReviewMediaRepo.Verify(r => r.AddAsync(It.Is<ReviewMedia>(m => m.MediaUrl == "https://cdn.example.com/new.jpg"), It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.Type == "REVIEW_UPDATED"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID05 - Empty OrderId resolves from existing package review fallback and succeeds")]
    public async Task UTCID05_SubmitPackageReviewAsync_EmptyOrderIdWithExistingPackageReview_ResolvesOrderIdAndSucceeds()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var resolvedOrderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(resolvedOrderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var legacyReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = resolvedOrderId,
            TraineeId = traineeId,
            Rating = 4,
            IsEdited = false
        };

        var dto = new CreatePackageReviewDto
        {
            OrderId = Guid.Empty,
            Rating = 5,
            Comment = "Resolved via fallback",
            ImageUrls = null!
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legacyReview);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(resolvedOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(resolvedOrderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legacyReview);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(resolvedOrderId, dto.OrderId);
        Assert.True(legacyReview.IsEdited);
    }

    [Fact(DisplayName = "UTCID06 - Notification or email exception does not fail review submission")]
    public async Task UTCID06_SubmitPackageReviewAsync_WhenEmailThrows_StillReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Great!", ImageUrls = null! };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP / User lookup error"));

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Review submitted successfully.", result.Message);
    }

    [Fact(DisplayName = "UTCID07 - Unique constraint violation on save falls back to updating concurrent unedited review")]
    public async Task UTCID07_SubmitPackageReviewAsync_UniqueConstraintViolation_UpdatesConcurrentReview()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var concurrentReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            TraineeId = traineeId,
            Rating = 3,
            IsEdited = false
        };
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Concurrent update", ImageUrls = null! };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.SetupSequence(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null)
            .ReturnsAsync(concurrentReview);

        _mockUow.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("ERROR: 23505 duplicate key value violates unique constraint"))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, concurrentReview.Rating);
        Assert.True(concurrentReview.IsEdited);
        _mockReviewRepo.Verify(r => r.Update(concurrentReview), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID08 - Lower boundary Rating = 1 with empty comment succeeds")]
    public async Task UTCID08_SubmitPackageReviewAsync_MinimumRatingOneAndEmptyComment_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 1,
            Comment = string.Empty,
            ImageUrls = new List<string>()
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev => rev.Rating == 1 && rev.Comment == string.Empty), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 - Upper boundary of exactly 5 images succeeds")]
    public async Task UTCID09_SubmitPackageReviewAsync_ExactlyFiveImages_ReturnsOkAndSavesAllFive()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Five images boundary",
            ImageUrls = new List<string> { "img1.jpg", "img2.jpg", "img3.jpg", "img4.jpg", "img5.jpg" }
        };

        var savedReview = new Review { ReviewId = Guid.NewGuid(), OrderId = orderId, PackageId = packageId, TraineeId = traineeId, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.SetupSequence(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null)
            .ReturnsAsync(savedReview);
        _mockReviewMediaRepo.Setup(r => r.GetMediaByReviewIdAsync(savedReview.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewMedia>());

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        _mockReviewMediaRepo.Verify(r => r.AddAsync(It.IsAny<ReviewMedia>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact(DisplayName = "UTCID10 - Adjacent boundary of 6 images (> 5) throws ValidationException")]
    public async Task UTCID10_SubmitPackageReviewAsync_SixImagesExceedingLimit_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Too many images",
            ImageUrls = new List<string> { "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg", "6.jpg" }
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Maximum 5 images allowed per review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID11 - Coach profile with APPROVED status and null Status property is treated as active")]
    public async Task UTCID11_SubmitPackageReviewAsync_CoachStatusNullAndApproved_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId, CoachConstants.ApprovalStatusApproved, status: null);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Approved coach with null status", ImageUrls = null! };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID12 - Non-existent training package throws NotFoundException")]
    public async Task UTCID12_SubmitPackageReviewAsync_PackageNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Training package not found.", ex.Message);
    }

    [Fact(DisplayName = "UTCID13 - Unapproved coach profile (PENDING) throws ValidationException")]
    public async Task UTCID13_SubmitPackageReviewAsync_CoachNotApproved_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreateEligiblePackage(packageId, coachId, approvalStatus: "PENDING", status: CoachConstants.StatusActive);
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Coach of this package is not available for review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID14 - Suspended coach profile throws ValidationException")]
    public async Task UTCID14_SubmitPackageReviewAsync_CoachSuspended_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreateEligiblePackage(packageId, coachId, approvalStatus: CoachConstants.ApprovalStatusApproved, status: "SUSPENDED");
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Coach of this package is not available for review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID15 - Empty OrderId with no existing review fallback throws ValidationException")]
    public async Task UTCID15_SubmitPackageReviewAsync_EmptyOrderIdAndNoFallback_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreateEligiblePackage(packageId, coachId);
        var dto = new CreatePackageReviewDto { OrderId = Guid.Empty, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("OrderId is required to submit a review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID16 - Order not found or belongs to another trainee throws ValidationException")]
    public async Task UTCID16_SubmitPackageReviewAsync_OrderBelongsToDifferentBuyer_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var otherBuyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, otherBuyerId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Order not found or does not belong to you.", ex.Message);
    }

    [Fact(DisplayName = "UTCID17 - Unpaid order status (PENDING) throws ValidationException")]
    public async Task UTCID17_SubmitPackageReviewAsync_UnpaidOrder_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, orderStatus: "PENDING");
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("You can only review a package from a paid order.", ex.Message);
    }

    [Fact(DisplayName = "UTCID18 - Order does not contain the specified package throws ValidationException")]
    public async Task UTCID18_SubmitPackageReviewAsync_OrderMissingSpecifiedPackage_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var differentPackageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, differentPackageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5 };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("This order does not contain the specified package.", ex.Message);
    }

    [Fact(DisplayName = "UTCID19 - Editing a review that was already edited once throws ValidationException")]
    public async Task UTCID19_SubmitPackageReviewAsync_ReviewAlreadyEditedOnce_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var alreadyEditedReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            TraineeId = traineeId,
            Rating = 4,
            IsEdited = true
        };
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Second edit attempt" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyEditedReview);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("This review has been edited once and cannot be edited further.", ex.Message);
    }

    [Fact(DisplayName = "UTCID20 - DbUpdateConcurrencyException during SaveChangesAsync throws ValidationException")]
    public async Task UTCID20_SubmitPackageReviewAsync_ConcurrencyConflict_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreateEligiblePackage(packageId, coachId);
        var order = CreateEligibleOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var existingReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            TraineeId = traineeId,
            Rating = 4,
            IsEdited = false
        };
        var concurrentState = new Review
        {
            ReviewId = existingReview.ReviewId,
            PackageId = packageId,
            OrderId = orderId,
            TraineeId = traineeId,
            Rating = 4,
            IsEdited = false
        };
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Concurrent edit" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.SetupSequence(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReview)
            .ReturnsAsync(concurrentState);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Review was modified by another request. Please reload and try again.", ex.Message);
    }

    [Fact(DisplayName = "UTCID21 - Rating below lower boundary (Rating = 0) throws ValidationException")]
    public async Task UTCID21_SubmitPackageReviewAsync_RatingBelowMinimum_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 0, Comment = "Invalid zero rating" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Rating must be between 1 and 5.", ex.Message);
        _mockPackageRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID22 - Rating above upper boundary (Rating = 6) throws ValidationException")]
    public async Task UTCID22_SubmitPackageReviewAsync_RatingAboveMaximum_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 6, Comment = "Invalid six rating" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Rating must be between 1 and 5.", ex.Message);
        _mockPackageRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
