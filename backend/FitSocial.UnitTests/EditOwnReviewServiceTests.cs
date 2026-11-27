using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for Edit Own Review (CoachService.UpdateReviewAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID12).
/// </summary>
public class EditOwnReviewServiceTests
{
    private readonly Mock<ICoachProfileRepository> _mockCoachProfileRepo;
    private readonly Mock<ILocationRepository> _mockLocationRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly CoachService _coachService;

    public EditOwnReviewServiceTests()
    {
        _mockCoachProfileRepo = new Mock<ICoachProfileRepository>();
        _mockLocationRepo = new Mock<ILocationRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();

        _coachService = new CoachService(
            _mockCoachProfileRepo.Object,
            _mockLocationRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object);
    }

    private static Review CreateReview(
        Guid reviewId,
        Guid traineeId,
        int rating = 3,
        string? comment = "Initial review comment",
        bool? isEdited = false)
    {
        return new Review
        {
            ReviewId = reviewId,
            CoachId = Guid.NewGuid(),
            PackageId = Guid.NewGuid(),
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
    public async Task UTCID01_UpdateReviewAsync_WhenOwnUneditedReview_UpdatesFieldsAndReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 3, comment: "Old comment", isEdited: false);
        var dto = new UpdateReviewDto
        {
            Rating = 4,
            Comment = "Updated review after completing the program"
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

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
    public async Task UTCID02_UpdateReviewAsync_WhenIsEditedIsNull_UpdatesFieldsAndReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 4, comment: "Initial", isEdited: null);
        var dto = new UpdateReviewDto
        {
            Rating = 5,
            Comment = "Great experience"
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Great experience", review.Comment);
        Assert.True(review.IsEdited);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 - Edit own review with null comment clears comment and updates rating")]
    public async Task UTCID03_UpdateReviewAsync_WithNullComment_ClearsCommentAndReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 4, comment: "Previous text", isEdited: false);
        var dto = new UpdateReviewDto
        {
            Rating = 3,
            Comment = null
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, review.Rating);
        Assert.Null(review.Comment);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID04 - Custom CancellationToken is forwarded to repository and unit of work")]
    public async Task UTCID04_UpdateReviewAsync_ForwardsCancellationToken()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdateReviewDto { Rating = 5, Comment = "New" };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, token))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(token))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto, token);

        // Assert
        Assert.True(result.Success);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(reviewId, token), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID05 - Lower boundary Rating = 1 and empty string comment succeeds")]
    public async Task UTCID05_UpdateReviewAsync_MinimumRatingOneAndEmptyComment_ReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 4, comment: "Old", isEdited: false);
        var dto = new UpdateReviewDto
        {
            Rating = 1,
            Comment = string.Empty
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, review.Rating);
        Assert.Equal(string.Empty, review.Comment);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID06 - Upper boundary Rating = 5 and Comment at exact 500-character max length succeeds")]
    public async Task UTCID06_UpdateReviewAsync_MaxRatingFiveAnd500CharComment_ReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 2, comment: "Old", isEdited: false);
        var maxComment = new string('X', 500);
        var dto = new UpdateReviewDto
        {
            Rating = 5,
            Comment = maxComment
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, review.Rating);
        Assert.Equal(500, review.Comment!.Length);
        Assert.True(review.IsEdited);
    }

    [Fact(DisplayName = "UTCID07 - Single-character Comment boundary (1 char) succeeds")]
    public async Task UTCID07_UpdateReviewAsync_SingleCharComment_ReturnsOk()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdateReviewDto
        {
            Rating = 5,
            Comment = "A"
        };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("A", review.Comment);
        Assert.True(review.IsEdited);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID08 - Review not found throws NotFoundException")]
    public async Task UTCID08_UpdateReviewAsync_WhenReviewNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new UpdateReviewDto { Rating = 5, Comment = "Updated" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _coachService.UpdateReviewAsync(reviewId, traineeId, dto));
        Assert.Equal("Review not found.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID09 - Editing another trainee's review throws UnauthorizedAccessException")]
    public async Task UTCID09_UpdateReviewAsync_WhenDifferentTrainee_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var ownerTraineeId = Guid.NewGuid();
        var otherTraineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, ownerTraineeId, rating: 4, comment: "Owner comment", isEdited: false);
        var dto = new UpdateReviewDto { Rating = 1, Comment = "Unauthorized edit" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _coachService.UpdateReviewAsync(reviewId, otherTraineeId, dto));
        Assert.Equal("You can only edit your own review.", ex.Message);
        Assert.Equal(4, review.Rating);
        Assert.Equal("Owner comment", review.Comment);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID10 - Editing a review that was already edited once (IsEdited = true) throws ValidationException")]
    public async Task UTCID10_UpdateReviewAsync_WhenAlreadyEditedOnce_ThrowsValidationException()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 4, comment: "First edit", isEdited: true);
        var dto = new UpdateReviewDto { Rating = 5, Comment = "Second edit attempt" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(reviewId, traineeId, dto));
        Assert.Equal("This review has been edited once and cannot be edited further.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID11 - DbUpdateConcurrencyException during SaveChangesAsync throws ValidationException")]
    public async Task UTCID11_UpdateReviewAsync_WhenConcurrencyConflict_ThrowsValidationException()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 3, comment: "Old", isEdited: false);
        var dto = new UpdateReviewDto { Rating = 5, Comment = "Concurrent update" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(reviewId, traineeId, dto));
        Assert.Equal("Review was modified by another request. Please reload and try again.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
    }

    [Fact(DisplayName = "UTCID12 - Repository throws database connection exception propagates exception")]
    public async Task UTCID12_UpdateReviewAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var dto = new UpdateReviewDto { Rating = 5, Comment = "Update" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _coachService.UpdateReviewAsync(reviewId, traineeId, dto));
        Assert.Equal("Database connection failed", ex.Message);
    }

    [Fact(DisplayName = "UTCID13 - Null UpdateReviewDto payload throws ValidationException")]
    public async Task UTCID13_UpdateReviewAsync_WhenDtoIsNull_ThrowsValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(Guid.NewGuid(), Guid.NewGuid(), null!));
        Assert.Equal("Invalid review payload.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = "UTCID14 - Rating outside 1..5 boundaries (0 and 6) throws ValidationException")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task UTCID14_UpdateReviewAsync_WhenRatingOutOfRange_ThrowsValidationException(int invalidRating)
    {
        var dto = new UpdateReviewDto { Rating = invalidRating, Comment = "Out of range rating" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(Guid.NewGuid(), Guid.NewGuid(), dto));
        Assert.Equal("Rating must be between 1 and 5.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID15 - Comment exceeding 500-character max length (501 chars) throws ValidationException")]
    public async Task UTCID15_UpdateReviewAsync_WhenCommentExceeds500Chars_ThrowsValidationException()
    {
        var dto = new UpdateReviewDto { Rating = 5, Comment = new string('X', 501) };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(Guid.NewGuid(), Guid.NewGuid(), dto));
        Assert.Equal("Comment cannot exceed 500 characters.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID16 - Editing review when associated order has PENDING or APPROVED refund throws ValidationException")]
    public async Task UTCID16_UpdateReviewAsync_WhenOrderHasPendingRefund_ThrowsValidationException()
    {
        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var review = CreateReview(reviewId, traineeId, rating: 4, comment: "Original", isEdited: false);
        var refundedOrder = new Order
        {
            OrderId = review.OrderId!.Value,
            BuyerId = traineeId,
            RefundRequests = new List<RefundRequest>
            {
                new() { OrderId = review.OrderId.Value, Status = "PENDING" }
            }
        };
        var dto = new UpdateReviewDto { Rating = 5, Comment = "Attempt edit during refund" };

        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);
        _mockOrderRepo.Setup(o => o.GetOrderByIdWithDetailsAsync(review.OrderId.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedOrder);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _coachService.UpdateReviewAsync(reviewId, traineeId, dto));
        Assert.Equal("Cannot submit or edit a review while a refund request is pending or approved.", ex.Message);
        _mockReviewRepo.Verify(r => r.Update(It.IsAny<Review>()), Times.Never);
    }

    #endregion
}

