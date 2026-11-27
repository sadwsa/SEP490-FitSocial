using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for Edit Own Review (TrainingPackageService.UpdatePackageReviewAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID19).
/// </summary>
public class EditOwnReviewServiceTests
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

    public EditOwnReviewServiceTests()
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

    private static Review CreateReview(
        Guid reviewId,
        Guid traineeId,
        Guid? packageId = null,
        int rating = 3,
        string? comment = "Initial review comment",
        bool? isEdited = false)
    {
        return new Review
        {
            ReviewId = reviewId,
            CoachId = Guid.NewGuid(),
            PackageId = packageId ?? Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            TraineeId = traineeId,
            Rating = rating,
            Comment = comment,
            IsEdited = isEdited,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    #region Normal Cases (N)

    [Fact(DisplayName = "UTCID01 - Edit own unedited review (IsEdited = false) updates rating, comment, and sets IsEdited = true")]
    public async Task UTCID01_UpdatePackageReviewAsync_WhenOwnUneditedReview_UpdatesFieldsAndReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 3, comment: "Old comment", isEdited: false);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 4,
            Comment = "Updated review after completing the program",
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Review updated successfully.", result.Message);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Updated review after completing the program", review.Comment);
        Assert.True(review.IsEdited);
        Assert.NotNull(review.UpdatedAt);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 - Edit own legacy review with IsEdited = null updates fields and sets IsEdited = true")]
    public async Task UTCID02_UpdatePackageReviewAsync_WhenIsEditedIsNull_UpdatesFieldsAndReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 4, comment: "Initial", isEdited: null);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 5,
            Comment = "Great experience",
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Great experience", review.Comment);
        Assert.True(review.IsEdited);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 - Edit own review with null comment clears comment and updates rating")]
    public async Task UTCID03_UpdatePackageReviewAsync_WithNullComment_ClearsCommentAndReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 4, comment: "Previous text", isEdited: false);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 3,
            Comment = null,
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, review.Rating);
        Assert.Null(review.Comment);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID04 - Custom CancellationToken is forwarded to repository and unit of work")]
    public async Task UTCID04_UpdatePackageReviewAsync_ForwardsCancellationToken()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "New", ImageUrls = null };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, token))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(token))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto, token);

        // Assert
        Assert.True(result.Success);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(reviewId, token), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    [Fact(DisplayName = "UTCID17 - Updating review replaces attached ReviewMedia and sends REVIEW_UPDATED notification")]
    public async Task UTCID17_UpdatePackageReviewAsync_ReplacesMediaAndSendsNotification()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 3, comment: "Old", isEdited: false);
        var oldMedia = new ReviewMedia { ReviewMediaId = Guid.NewGuid(), ReviewId = reviewId, MediaUrl = "https://cdn.example.com/old.jpg" };
        var dto = new UpdatePackageReviewDto
        {
            Rating = 5,
            Comment = "Updated with new photos",
            ImageUrls = new List<string> { "https://cdn.example.com/new.jpg" }
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockReviewMediaRepo.Setup(m => m.GetMediaByReviewIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReviewMedia> { oldMedia });
        _mockUserRepo.Setup(u => u.GetByIdAsync(review.CoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = review.CoachId, FullName = "Coach Alex", Email = "coach@example.com" });
        _mockUserRepo.Setup(u => u.GetByIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { UserId = traineeId, FullName = "Trainee Bo" });
        _mockPackageRepo.Setup(p => p.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrainingPackage { PackageId = packageId, CoachId = review.CoachId, Title = "Pro Cut" });

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        _mockReviewMediaRepo.Verify(m => m.Remove(oldMedia), Times.Once);
        _mockReviewMediaRepo.Verify(m => m.AddAsync(It.Is<ReviewMedia>(x => x.MediaUrl == "https://cdn.example.com/new.jpg"), It.IsAny<CancellationToken>()), Times.Once);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(x => x.Type == "REVIEW_UPDATED"), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID05 - Lower boundary Rating = 1 and empty string comment succeeds")]
    public async Task UTCID05_UpdatePackageReviewAsync_MinimumRatingOneAndEmptyComment_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 4, comment: "Old", isEdited: false);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 1,
            Comment = string.Empty,
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, review.Rating);
        Assert.Equal(string.Empty, review.Comment);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID06 - Upper boundary Rating = 5 and Comment at exact 1000-character max length succeeds")]
    public async Task UTCID06_UpdatePackageReviewAsync_MaxRatingFiveAnd1000CharComment_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 2, comment: "Old", isEdited: false);
        var maxComment = new string('X', 1000);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 5,
            Comment = maxComment,
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, review.Rating);
        Assert.Equal(1000, review.Comment!.Length);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID07 - Single-character Comment boundary (1 char) succeeds")]
    public async Task UTCID07_UpdatePackageReviewAsync_SingleCharComment_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdatePackageReviewDto
        {
            Rating = 5,
            Comment = "A",
            ImageUrls = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("A", review.Comment);
        Assert.True(review.IsEdited);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID08 - Review not found throws NotFoundException")]
    public async Task UTCID08_UpdatePackageReviewAsync_WhenReviewNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Updated" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto));
        Assert.Equal("Review not found.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 - Editing another trainee's review throws UnauthorizedAccessException")]
    public async Task UTCID09_UpdatePackageReviewAsync_WhenDifferentTrainee_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var ownerTraineeId = Guid.NewGuid();
        var otherTraineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, ownerTraineeId, packageId, rating: 4, comment: "Owner comment", isEdited: false);
        var dto = new UpdatePackageReviewDto { Rating = 1, Comment = "Unauthorized edit" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, otherTraineeId, dto));
        Assert.Equal("You can only edit your own review.", ex.Message);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Owner comment", review.Comment);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 - Editing a review that was already edited once (IsEdited = true) throws ValidationException")]
    public async Task UTCID10_UpdatePackageReviewAsync_WhenAlreadyEditedOnce_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 4, comment: "First edit", isEdited: true);
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Second edit attempt" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto));
        Assert.Equal("This review has been edited once and cannot be edited further.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID11 - DbUpdateConcurrencyException during SaveChangesAsync throws ValidationException")]
    public async Task UTCID11_UpdatePackageReviewAsync_WhenConcurrencyConflict_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Concurrent update" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto));
        Assert.Equal("Review was modified by another request. Please reload and try again.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
    }

    [Fact(DisplayName = "UTCID12 - Repository throws database connection exception propagates exception")]
    public async Task UTCID12_UpdatePackageReviewAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Update" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto));
        Assert.Equal("Database connection failed", ex.Message);
    }

    [Fact(DisplayName = "UTCID13 - Null UpdatePackageReviewDto payload throws ValidationException")]
    public async Task UTCID13_UpdatePackageReviewAsync_WhenDtoIsNull_ThrowsValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null!));
        Assert.Equal("Invalid review payload.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = "UTCID14 - Rating outside 1..5 boundaries (0 and 6) throws ValidationException")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task UTCID14_UpdatePackageReviewAsync_WhenRatingOutOfRange_ThrowsValidationException(int invalidRating)
    {
        var dto = new UpdatePackageReviewDto { Rating = invalidRating, Comment = "Out of range rating" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), dto));
        Assert.Equal("Rating must be between 1 and 5.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID15 - Comment exceeding 1000-character max length (1001 chars) throws ValidationException")]
    public async Task UTCID15_UpdatePackageReviewAsync_WhenCommentExceeds1000Chars_ThrowsValidationException()
    {
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = new string('X', 1001) };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), dto));
        Assert.Equal("Comment cannot exceed 1000 characters.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID16 - Editing review when associated order has PENDING or APPROVED refund throws ValidationException")]
    public async Task UTCID16_UpdatePackageReviewAsync_WhenOrderHasPendingRefund_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, packageId, rating: 4, comment: "Original", isEdited: false);
        var refundedOrder = new Order
        {
            OrderId = review.OrderId!.Value,
            BuyerId = traineeId,
            RefundRequests = new List<RefundRequest>
            {
                new() { OrderId = review.OrderId.Value, Status = "PENDING" }
            }
        };
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Attempt edit during refund" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockOrderRepo.Setup(o => o.GetOrderByIdWithDetailsAsync(review.OrderId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedOrder);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, dto));
        Assert.Equal("Cannot submit or edit a review while a refund request is pending or approved.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID18 - Exceeding 5 images or passing unsafe image URL throws ValidationException")]
    public async Task UTCID18_UpdatePackageReviewAsync_InvalidImages_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var tooManyDto = new UpdatePackageReviewDto
        {
            Rating = 5,
            ImageUrls = new List<string> { "https://a.com/1.jpg", "https://a.com/2.jpg", "https://a.com/3.jpg", "https://a.com/4.jpg", "https://a.com/5.jpg", "https://a.com/6.jpg" }
        };
        var exCount = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, tooManyDto));
        Assert.Equal("Maximum 5 images allowed per review.", exCount.Message);

        var unsafeDto = new UpdatePackageReviewDto
        {
            Rating = 5,
            ImageUrls = new List<string> { "javascript:alert(1)" }
        };
        var exUrl = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdatePackageReviewAsync(packageId, reviewId, traineeId, unsafeDto));
        Assert.Equal("Invalid review image URL.", exUrl.Message);
    }

    [Fact(DisplayName = "UTCID19 - Review belonging to a different package throws NotFoundException")]
    public async Task UTCID19_UpdatePackageReviewAsync_MismatchedPackageId_ThrowsNotFoundException()
    {
        var routePackageId = Guid.NewGuid();
        var actualPackageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, actualPackageId, rating: 4, comment: "Original", isEdited: false);
        var dto = new UpdatePackageReviewDto { Rating = 5, Comment = "Wrong package" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdatePackageReviewAsync(routePackageId, reviewId, traineeId, dto));
        Assert.Equal("Review not found.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
    }

    #endregion
}
