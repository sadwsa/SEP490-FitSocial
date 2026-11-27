using System;
using System;
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
/// Unit tests for Coach Reply to Trainee Review (TrainingPackageService.ReplyToReviewAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID16).
/// </summary>
public class CoachReplyToTraineeReviewServiceTests
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

    public CoachReplyToTraineeReviewServiceTests()
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

    private static TrainingPackage CreatePackage(Guid packageId, Guid coachId, string? title = "HIIT Fat Loss Program")
    {
        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = title!,
            Price = 1000000m,
            IsActive = true
        };
    }

    private static Review CreateReview(Guid reviewId, Guid packageId, Guid coachId, Guid traineeId, string? reply = null)
    {
        return new Review
        {
            ReviewId = reviewId,
            PackageId = packageId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = 5,
            Comment = "Great training sessions!",
            Reply = reply,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    #region Normal Cases (N)

    [Fact(DisplayName = "UTCID01 - Coach who owns package replies to unreplied review, sends notification and email")]
    public async Task UTCID01_ReplyToReviewAsync_ValidReply_UpdatesReviewAndSendsNotificationAndEmail()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId, "HIIT Fat Loss Program");
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var trainee = new User { UserId = traineeId, FullName = "Trainee Hoa", Email = "hoa@example.com" };
        var dto = new ReplyReviewDto { Reply = "Thank you for your hard work and feedback!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(trainee);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Reply posted successfully.", result.Message);
        Assert.Equal("Thank you for your hard work and feedback!", review.Reply);
        Assert.NotNull(review.UpdatedAt);
        _mockReviewRepo.Verify(r => r.Update(review), Times.Once);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.Is<Notification>(notif =>
            notif.UserId == traineeId &&
            notif.ActorId == coachId &&
            notif.ReferenceId == reviewId &&
            notif.Type == "REVIEW_REPLY"), It.IsAny<CancellationToken>()), Times.Once);
        _mockEmailService.Verify(e => e.SendEmailAsync(
            "hoa@example.com",
            "[FitConnect] Coach replied to your review",
            It.Is<string>(body => body.Contains("Trainee Hoa") && body.Contains("Thank you for your hard work and feedback!"))), Times.Once);
    }

    [Fact(DisplayName = "UTCID02 - Coach replies when trainee FullName and package Title are null uses fallback labels in email")]
    public async Task UTCID02_ReplyToReviewAsync_NullTraineeNameAndNullPackageTitle_UsesFallbackEmailLabels()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId, title: null);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: string.Empty);
        var trainee = new User { UserId = traineeId, FullName = null, Email = "trainee@example.com" };
        var dto = new ReplyReviewDto { Reply = "Keep up the great progress!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(trainee);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Keep up the great progress!", review.Reply);
        _mockEmailService.Verify(e => e.SendEmailAsync(
            "trainee@example.com",
            "[FitConnect] Coach replied to your review",
            It.Is<string>(body => body.Contains("Hi Customer,") && body.Contains("this package"))), Times.Once);
    }

    [Fact(DisplayName = "UTCID03 - Coach replies when trainee user record is null skips notification and email")]
    public async Task UTCID03_ReplyToReviewAsync_WhenTraineeNotFound_SavesReplyAndSkipsNotification()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "Thank you!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Thank you!", review.Reply);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID04 - Coach replies when trainee email is empty creates notification but skips email")]
    public async Task UTCID04_ReplyToReviewAsync_WhenTraineeEmailEmpty_CreatesNotificationAndSkipsEmail()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var trainee = new User { UserId = traineeId, FullName = "Trainee", Email = "   " };
        var dto = new ReplyReviewDto { Reply = "Appreciated!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>())).ReturnsAsync(trainee);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        _mockNotificationRepo.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID05 - Notification or email exception is caught and does not fail reply submission")]
    public async Task UTCID05_ReplyToReviewAsync_WhenEmailServiceThrows_StillReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "Thank you very much!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUserRepo.Setup(r => r.GetByIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification failure"));

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Reply posted successfully.", result.Message);
        Assert.Equal("Thank you very much!", review.Reply);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID06 - Lower boundary 1-character reply succeeds")]
    public async Task UTCID06_ReplyToReviewAsync_SingleCharacterReply_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "A" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("A", review.Reply);
    }

    [Fact(DisplayName = "UTCID07 - Reply with leading and trailing whitespace is trimmed before saving")]
    public async Task UTCID07_ReplyToReviewAsync_PaddedWhitespaceReply_TrimsAndSaves()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "   Thanks for training with me!   " };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Thanks for training with me!", review.Reply);
    }

    [Fact(DisplayName = "UTCID08 - Upper boundary 1000-character reply succeeds")]
    public async Task UTCID08_ReplyToReviewAsync_Max1000CharacterReply_ReturnsOk()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var maxReply = new string('R', 1000);
        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = maxReply };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1000, review.Reply!.Length);
    }

    [Fact(DisplayName = "UTCID09 - Existing whitespace-only review.Reply is treated as unreplied and allows reply")]
    public async Task UTCID09_ReplyToReviewAsync_WhenExistingReplyIsWhitespaceOnly_AllowsReply()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: "   ");
        var dto = new ReplyReviewDto { Reply = "First actual reply" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);

        // Act
        var result = await _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("First actual reply", review.Reply);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID10 - Non-existent package throws NotFoundException")]
    public async Task UTCID10_ReplyToReviewAsync_WhenPackageNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var dto = new ReplyReviewDto { Reply = "Thank you!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrainingPackage?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Package not found.", ex.Message);
    }

    [Fact(DisplayName = "UTCID11 - Coach who does not own the package throws UnauthorizedAccessException")]
    public async Task UTCID11_ReplyToReviewAsync_WhenCoachDoesNotOwnPackage_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var ownerCoachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();

        var package = CreatePackage(packageId, ownerCoachId);
        var dto = new ReplyReviewDto { Reply = "Unauthorized reply" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, otherCoachId, dto));
        Assert.Equal("Only the coach who owns this package can reply to its reviews.", ex.Message);
    }

    [Fact(DisplayName = "UTCID12 - Empty or whitespace Reply throws ValidationException")]
    public async Task UTCID12_ReplyToReviewAsync_WhenReplyIsEmptyOrWhitespace_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var dto = new ReplyReviewDto { Reply = "   " };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Reply content cannot be empty.", ex.Message);
    }

    [Fact(DisplayName = "UTCID13 - Non-existent review throws NotFoundException")]
    public async Task UTCID13_ReplyToReviewAsync_WhenReviewNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var dto = new ReplyReviewDto { Reply = "Thank you!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync((Review?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Review not found for this package.", ex.Message);
    }

    [Fact(DisplayName = "UTCID14 - Review belonging to a different package throws NotFoundException")]
    public async Task UTCID14_ReplyToReviewAsync_WhenReviewBelongsToDifferentPackage_ThrowsNotFoundException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var otherPackageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var reviewOnOtherPackage = CreateReview(reviewId, otherPackageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "Thank you!" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(reviewOnOtherPackage);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Review not found for this package.", ex.Message);
    }

    [Fact(DisplayName = "UTCID15 - Review that already has a reply throws ValidationException")]
    public async Task UTCID15_ReplyToReviewAsync_WhenReviewAlreadyHasReply_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var alreadyRepliedReview = CreateReview(reviewId, packageId, coachId, traineeId, reply: "Existing coach reply");
        var dto = new ReplyReviewDto { Reply = "Second reply attempt" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(alreadyRepliedReview);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("This review already has a reply. You cannot reply again.", ex.Message);
    }

    [Fact(DisplayName = "UTCID16 - DbUpdateConcurrencyException during SaveChangesAsync throws ValidationException")]
    public async Task UTCID16_ReplyToReviewAsync_WhenConcurrencyConflict_ThrowsValidationException()
    {
        // Arrange
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();

        var package = CreatePackage(packageId, coachId);
        var review = CreateReview(reviewId, packageId, coachId, traineeId, reply: null);
        var dto = new ReplyReviewDto { Reply = "Concurrent reply" };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);
        _mockReviewRepo.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Review was modified by another request. Please reload and try again.", ex.Message);
    }

    [Fact(DisplayName = "UTCID17 - Null ReplyReviewDto payload throws ValidationException")]
    public async Task UTCID17_ReplyToReviewAsync_WhenDtoIsNull_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId);

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, null!));
        Assert.Equal("Reply content cannot be empty.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName = "UTCID18 - Reply exceeding 1000-character limit (1001 chars) throws ValidationException")]
    public async Task UTCID18_ReplyToReviewAsync_WhenReplyExceeds1000Chars_ThrowsValidationException()
    {
        var packageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var package = CreatePackage(packageId, coachId);
        var dto = new ReplyReviewDto { Reply = new string('R', 1001) };

        _mockPackageRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReplyToReviewAsync(packageId, reviewId, coachId, dto));
        Assert.Equal("Reply cannot exceed 1000 characters.", ex.Message);
        _mockReviewRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}

