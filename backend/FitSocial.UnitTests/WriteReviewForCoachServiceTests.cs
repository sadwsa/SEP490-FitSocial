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
/// Unit tests for Write Review for Coach (TrainingPackageService.SubmitPackageReviewAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID20).
/// </summary>
public class WriteReviewForCoachServiceTests
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

    public WriteReviewForCoachServiceTests()
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

    private static TrainingPackage CreatePackage(
        Guid packageId,
        Guid coachId,
        string? approvalStatus = CoachConstants.ApprovalStatusApproved,
        string? status = CoachConstants.StatusActive)
    {
        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Strength & Conditioning Plan",
            Price = 1200000m,
            IsActive = true,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                ApprovalStatus = approvalStatus,
                Status = status
            }
        };
    }

    private static Order CreateOrder(
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

    [Fact(DisplayName = "UTCID01 - Write new review with comment and 2 images on PAID order succeeds")]
    public async Task UTCID01_WriteReview_NewReviewWithCommentAndImages_PersistsReviewMediaAndSendsEmail()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Great coaching sessions and personalized meal plan!",
            ImageUrls = new List<string> { "https://cdn.example.com/photo1.jpg", "https://cdn.example.com/photo2.jpg" }
        };

        var savedReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            OrderId = orderId,
            PackageId = packageId,
            TraineeId = traineeId,
            Rating = 5,
            Comment = dto.Comment
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.SetupSequence(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null)
            .ReturnsAsync(savedReview);
        _mockReviewMediaRepo.Setup(r => r.GetMediaByReviewIdAsync(savedReview.ReviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewMedia>());
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = coachId, FullName = "Coach Minh", Email = "minh.coach@example.com" });
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = traineeId, FullName = "Trainee Lan" });

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Review submitted successfully.", result.Message);
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev =>
            rev.Comment == "Great coaching sessions and personalized meal plan!" &&
            rev.Rating == 5 &&
            rev.IsEdited == false), It.IsAny<CancellationToken>()), Times.Once);
        _mockReviewMediaRepo.Verify(r => r.AddAsync(It.IsAny<ReviewMedia>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockEmailService.Verify(e => e.SendEmailAsync(
            "minh.coach@example.com",
            It.Is<string>(s => s.Contains("New review")),
            It.Is<string>(body => body.Contains("Great coaching sessions and personalized meal plan!"))), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 - Write text-only review without images on COMPLETED order succeeds")]
    public async Task UTCID02_WriteReview_TextOnlyOnCompletedOrder_PersistsCommentWithoutMedia()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusCompleted);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 4,
            Comment = "Very helpful trainer, clear instructions.",
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
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev =>
            rev.Comment == "Very helpful trainer, clear instructions." &&
            rev.Rating == 4), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 - Write review on ACTIVE order with null ImageUrls and unnamed trainee uses default customer name")]
    public async Task UTCID03_WriteReview_ActiveOrderAndNullTraineeName_UsesDefaultCustomerNameInNotification()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusActive);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Solid workout schedule",
            ImageUrls = null!
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = coachId, FullName = null, Email = "coach@example.com" });
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        _mockNotificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n =>
            n.Description != null && n.Description.Contains("A customer")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID04 - Submitting a duplicate review via POST when an unedited review already exists throws ValidationException")]
    public async Task UTCID04_WriteReview_ExistingUneditedReview_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var existingReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = 3,
            Comment = "Initial short review",
            IsEdited = false
        };

        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Duplicate write attempt"
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReview);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("You have already reviewed this order. Please edit your existing review instead.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 - Empty OrderId throws ValidationException")]
    public async Task UTCID05_WriteReview_EmptyOrderId_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId);
        var dto = new CreatePackageReviewDto
        {
            OrderId = Guid.Empty,
            Rating = 5,
            Comment = "Missing order",
            ImageUrls = null!
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("OrderId is required to submit a review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID06 - Notification/email failure is caught and does not fail review writing")]
    public async Task UTCID06_WriteReview_NotificationThrows_StillReturnsSuccess()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Resilient comment", ImageUrls = null! };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Notification service unavailable"));

        // Act
        var result = await _service.SubmitPackageReviewAsync(packageId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Review submitted successfully.", result.Message);
    }

    [Fact(DisplayName = "UTCID07 - Unique constraint violation on duplicate concurrent POST throws ValidationException")]
    public async Task UTCID07_WriteReview_UniqueConstraintViolation_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Second concurrent comment", ImageUrls = null! };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("23505 duplicate key value violates unique constraint"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("You have already reviewed this order. Please edit your existing review instead.", ex.Message);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID08 - Boundary empty string comment with minimum Rating = 1 succeeds")]
    public async Task UTCID08_WriteReview_EmptyCommentAndMinRating_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
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
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev => rev.Comment == string.Empty && rev.Rating == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID09 - Boundary Comment at exact maximum length of 1000 characters succeeds")]
    public async Task UTCID09_WriteReview_CommentAtMax1000Characters_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var maxComment = new string('A', 1000);
        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = maxComment,
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
        _mockReviewRepo.Verify(r => r.AddAsync(It.Is<Review>(rev => rev.Comment != null && rev.Comment.Length == 1000), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 - Upper boundary of exactly 5 review images succeeds")]
    public async Task UTCID10_WriteReview_FiveImagesUpperBoundary_SavesAllFiveImages()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Review with 5 images",
            ImageUrls = new List<string> { "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg" }
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

    [Fact(DisplayName = "UTCID11 - Adjacent boundary of 6 review images (> 5) throws ValidationException")]
    public async Task UTCID11_WriteReview_SixImagesExceedingMax_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Too many review images",
            ImageUrls = new List<string> { "1.jpg", "2.jpg", "3.jpg", "4.jpg", "5.jpg", "6.jpg" }
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Maximum 5 images allowed per review.", ex.Message);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID12 - Non-existent package throws NotFoundException")]
    public async Task UTCID12_WriteReview_PackageNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Training package not found.", ex.Message);
    }

    [Fact(DisplayName = "UTCID13 - Coach profile is REJECTED throws ValidationException")]
    public async Task UTCID13_WriteReview_CoachRejected_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId, approvalStatus: "REJECTED", status: CoachConstants.StatusActive);
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Coach of this package is not available for review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID14 - Coach profile is INACTIVE throws ValidationException")]
    public async Task UTCID14_WriteReview_CoachInactive_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId, approvalStatus: CoachConstants.ApprovalStatusApproved, status: "INACTIVE");
        var dto = new CreatePackageReviewDto { OrderId = Guid.NewGuid(), Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Coach of this package is not available for review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID15 - Empty OrderId without fallback review throws ValidationException")]
    public async Task UTCID15_WriteReview_EmptyOrderIdWithoutFallback_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId);
        var dto = new CreatePackageReviewDto { OrderId = Guid.Empty, Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("OrderId is required to submit a review.", ex.Message);
    }

    [Fact(DisplayName = "UTCID16 - Order belongs to another trainee throws ValidationException")]
    public async Task UTCID16_WriteReview_OrderBelongsToAnotherBuyer_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var otherBuyerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, otherBuyerId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Order not found or does not belong to you.", ex.Message);
    }

    [Fact(DisplayName = "UTCID17 - Cancelled or unpaid order throws ValidationException")]
    public async Task UTCID17_WriteReview_CancelledOrder_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, orderStatus: "CANCELLED");
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("You can only review a package from a paid order.", ex.Message);
    }

    [Fact(DisplayName = "UTCID18 - Order does not contain the specified package throws ValidationException")]
    public async Task UTCID18_WriteReview_OrderMissingTargetPackage_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var otherPackageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, otherPackageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Review" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("This order does not contain the specified package.", ex.Message);
    }

    [Fact(DisplayName = "UTCID19 - Submitting via POST when review already exists throws ValidationException")]
    public async Task UTCID19_WriteReview_AlreadyExists_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var editedReview = new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = packageId,
            OrderId = orderId,
            TraineeId = traineeId,
            Rating = 4,
            Comment = "Edited once already",
            IsEdited = true
        };
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Second submit attempt" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(editedReview);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("You have already reviewed this order. Please edit your existing review instead.", ex.Message);
    }

    [Fact(DisplayName = "UTCID20 - DbUpdateConcurrencyException during SaveChangesAsync throws ValidationException")]
    public async Task UTCID20_WriteReview_ConcurrencyException_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto { OrderId = orderId, Rating = 5, Comment = "Concurrent write" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _mockReviewRepo.Setup(r => r.GetReviewByOrderIdAndTraineeAsync(orderId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Review was modified by another request. Please reload and try again.", ex.Message);
    }

    [Fact(DisplayName = "UTCID21 - Comment exceeding 1000-character limit (1001 chars) throws ValidationException")]
    public async Task UTCID21_WriteReview_CommentExceedingMax1000Chars_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new CreatePackageReviewDto
        {
            OrderId = Guid.NewGuid(),
            Rating = 5,
            Comment = new string('A', 1001)
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Comment cannot exceed 1000 characters.", ex.Message);
        _mockPackageRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID22 - Unsafe javascript scheme in ImageUrls throws ValidationException")]
    public async Task UTCID22_WriteReview_UnsafeJavascriptSchemeInImageUrls_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var order = CreateOrder(orderId, traineeId, packageId, PaymentConstants.OrderStatusPaid);
        var dto = new CreatePackageReviewDto
        {
            OrderId = orderId,
            Rating = 5,
            Comment = "Review with unsafe URL",
            ImageUrls = new List<string> { "javascript:alert(1)" }
        };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockOrderRepo.Setup(r => r.GetOrderByIdWithDetailsAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitPackageReviewAsync(packageId, traineeId, dto));
        Assert.Equal("Invalid review image URL.", ex.Message);
    }

    #endregion
}
