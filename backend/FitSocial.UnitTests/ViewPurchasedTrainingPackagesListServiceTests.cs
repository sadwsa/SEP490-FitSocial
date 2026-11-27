using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for View Purchased Training Packages List (TrainingPackageService.GetPurchasedPackagesAsync).
/// Covers Normal (N), Boundary (B), and Abnormal (A) test cases (UTCID01 - UTCID22).
/// </summary>
public class ViewPurchasedTrainingPackagesListServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _mockPackageRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly TrainingPackageService _service;

    public ViewPurchasedTrainingPackagesListServiceTests()
    {
        _mockPackageRepo = new Mock<ITrainingPackageRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockOrderRepo = new Mock<IOrderRepository>();

        _service = new TrainingPackageService(
            _mockPackageRepo.Object,
            _mockUow.Object,
            _mockReviewRepo.Object,
            _mockOrderRepo.Object);
    }

    private static OrderDetail CreateOrderDetail(
        Guid traineeId,
        Guid? orderId = null,
        Guid? packageId = null,
        string? orderStatus = "PAID",
        string? odPackageType = "BOTH",
        string? pkgPackageType = "BOTH")
    {
        var ordId = orderId ?? Guid.NewGuid();
        var pkgId = packageId ?? Guid.NewGuid();
        var coachId = Guid.NewGuid();

        var pkg = new TrainingPackage
        {
            PackageId = pkgId,
            CoachId = coachId,
            Title = "Strength & Conditioning 12 Weeks",
            Description = "Comprehensive strength program",
            Price = 1500000m,
            DurationDays = 84,
            SessionCount = 36,
            MinAge = 18,
            TargetAudience = "Intermediate",
            PackageType = pkgPackageType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User { UserId = coachId, FullName = "Coach Nguyen Van A" }
            },
            Reviews = new List<Review>(),
            Media = new List<TrainingPackageMedium>()
        };

        var order = new Order
        {
            OrderId = ordId,
            BuyerId = traineeId,
            OrderStatus = orderStatus,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
            RefundRequests = new List<RefundRequest>()
        };

        return new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = ordId,
            PackageId = pkgId,
            PackageType = odPackageType,
            Package = pkg,
            Order = order,
            TrainingPlans = new List<TrainingPlan>(),
            MealPlans = new List<MealPlan>()
        };
    }

    #region Normal Cases (N)

    [Fact(DisplayName = "UTCID01 - Default query maps purchased package, latest training/meal plans, and unreviewed state")]
    public async Task UTCID01_GetPurchasedPackagesAsync_WithPlansAndNoReview_MapsAllFieldsCorrectly()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);
        var olderPlanId = Guid.NewGuid();
        var newerPlanId = Guid.NewGuid();
        od.TrainingPlans.Add(new TrainingPlan { TrainingPlanId = olderPlanId, Status = "DRAFT", CreatedAt = DateTime.UtcNow.AddDays(-5) });
        od.TrainingPlans.Add(new TrainingPlan { TrainingPlanId = newerPlanId, Status = "ACTIVE", CreatedAt = DateTime.UtcNow.AddDays(-1) });

        var newerMealPlanId = Guid.NewGuid();
        od.MealPlans.Add(new MealPlan { MealPlanId = newerMealPlanId, Status = "PUBLISHED", CreatedAt = DateTime.UtcNow });

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var item = result.Items[0];
        Assert.Equal(od.OrderDetailsId, item.OrderDetailsId);
        Assert.Equal(od.OrderId, item.OrderId);
        Assert.Equal("PAID", item.OrderStatus);
        Assert.Equal("Coach Nguyen Van A", item.CoachName);
        Assert.Equal(newerPlanId, item.TrainingPlanId);
        Assert.Equal("ACTIVE", item.PlanStatus);
        Assert.Equal(newerMealPlanId, item.MealPlanId);
        Assert.Equal("PUBLISHED", item.MealPlanStatus);
        Assert.False(item.HasReviewed);
        Assert.False(item.IsReviewEdited);
        Assert.Empty(item.UserReviewImageUrls);
    }

    [Fact(DisplayName = "UTCID02 - Purchased package with existing user review maps rating, comment, IsEdited, and sorted media")]
    public async Task UTCID02_GetPurchasedPackagesAsync_WithUserReviewAndMedia_MapsReviewDetailsInSortOrder()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);
        var review = new Review
        {
            ReviewId = Guid.NewGuid(),
            TraineeId = traineeId,
            OrderId = od.OrderId,
            PackageId = od.PackageId,
            Rating = 5,
            Comment = "Awesome training plan!",
            IsEdited = true,
            ReviewMedia = new List<ReviewMedia>
            {
                new() { ReviewMediaId = Guid.NewGuid(), MediaUrl = "https://cdn.example.com/2.jpg", SortOrder = 2 },
                new() { ReviewMediaId = Guid.NewGuid(), MediaUrl = "https://cdn.example.com/1.jpg", SortOrder = 1 }
            }
        };
        od.Package!.Reviews.Add(review);

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.True(item.HasReviewed);
        Assert.True(item.IsReviewEdited);
        Assert.Equal(5, item.UserRating);
        Assert.Equal("Awesome training plan!", item.UserComment);
        Assert.Equal(new[] { "https://cdn.example.com/1.jpg", "https://cdn.example.com/2.jpg" }, item.UserReviewImageUrls);
    }

    [Fact(DisplayName = "UTCID03 - Order with multiple RefundRequests maps latest RefundRequest status")]
    public async Task UTCID03_GetPurchasedPackagesAsync_WithMultipleRefundRequests_SelectsLatestRefundStatus()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, orderStatus: "PAID");
        od.Order!.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            Status = "PENDING",
            RequestedAt = DateTime.UtcNow.AddDays(-3)
        });
        od.Order.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            Status = "REJECTED",
            RequestedAt = null,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("REJECTED", item.RefundStatus);
    }

    [Fact(DisplayName = "UTCID04 - Order with OrderStatus REFUND_PENDING and no RefundRequests maps RefundStatus to PENDING")]
    public async Task UTCID04_GetPurchasedPackagesAsync_WhenOrderStatusIsRefundPending_MapsRefundStatusPending()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, orderStatus: "refund_pending");

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("PENDING", item.RefundStatus);
    }

    [Fact(DisplayName = "UTCID05 - Order with OrderStatus REFUNDED and no RefundRequests maps RefundStatus to APPROVED")]
    public async Task UTCID05_GetPurchasedPackagesAsync_WhenOrderStatusIsRefunded_MapsRefundStatusApproved()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, orderStatus: "REFUNDED");

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("APPROVED", item.RefundStatus);
    }

    [Fact(DisplayName = "UTCID06 - PackageType fallback chain resolves OrderDetail.PackageType, Package.PackageType, or BOTH")]
    public async Task UTCID06_GetPurchasedPackagesAsync_PackageTypeFallbackChain_MapsCorrectly()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od1 = CreateOrderDetail(traineeId, odPackageType: "WORKOUT", pkgPackageType: "MEAL");
        var od2 = CreateOrderDetail(traineeId, odPackageType: null, pkgPackageType: null);

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od1, od2 }, 2));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("WORKOUT", result.Items[0].PackageType);
        Assert.Equal("BOTH", result.Items[1].PackageType);
    }

    [Fact(DisplayName = "UTCID07 - Multi-page query (pageNumber = 2, pageSize = 5) returns expected pagination metadata")]
    public async Task UTCID07_GetPurchasedPackagesAsync_SecondPage_ReturnsCorrectPagingMetadata()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 12));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 2, 5);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(12, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact(DisplayName = "UTCID08 - Boundary 0 purchased packages returns empty PagedResultDto")]
    public async Task UTCID08_GetPurchasedPackagesAsync_WhenNoPurchasedPackages_ReturnsEmptyResult()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID09 - Lower boundary pageNumber = 1 and pageSize = 1 forwards exact values")]
    public async Task UTCID09_GetPurchasedPackagesAsync_MinValidPageOneAndSizeOne_ForwardsUnchanged()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 1);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID10 - Boundary pageNumber = 0 is clamped to 1")]
    public async Task UTCID10_GetPurchasedPackagesAsync_WhenPageNumberIsZero_ClampsToOne()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 0, 10);

        // Assert
        Assert.Equal(1, result.PageNumber);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID11 - Boundary pageSize = 0 is clamped to default 10")]
    public async Task UTCID11_GetPurchasedPackagesAsync_WhenPageSizeIsZero_ClampsToTen()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 0);

        // Assert
        Assert.Equal(10, result.PageSize);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID12 - Exact upper boundary pageSize = 50 is kept as 50")]
    public async Task UTCID12_GetPurchasedPackagesAsync_WhenPageSizeIsFifty_KeepsFifty()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 50);

        // Assert
        Assert.Equal(50, result.PageSize);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID13 - Adjacent above-upper-boundary pageSize = 51 is clamped to 50")]
    public async Task UTCID13_GetPurchasedPackagesAsync_WhenPageSizeIsFiftyOne_ClampsToFifty()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 51);

        // Assert
        Assert.Equal(50, result.PageSize);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact(DisplayName = "UTCID14 - Negative pageNumber (-3) and pageSize (-10) are clamped to 1 and 10")]
    public async Task UTCID14_GetPurchasedPackagesAsync_NegativePagingValues_ClampsToDefaults()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, -3, -10);

        // Assert
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID15 - Null navigation properties (Coach, Order, Plans, Reviews, Media) map safely to defaults")]
    public async Task UTCID15_GetPurchasedPackagesAsync_WithNullNavigationProperties_MapsDefaultValuesSafely()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);
        od.Order = null!;
        od.TrainingPlans = null!;
        od.MealPlans = null!;
        od.Package!.Coach = null!;
        od.Package.Reviews = null!;
        od.Package.Media = null!;

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Unknown Coach", item.CoachName);
        Assert.Null(item.OrderStatus);
        Assert.Null(item.RefundStatus);
        Assert.Null(item.TrainingPlanId);
        Assert.Null(item.MealPlanId);
        Assert.False(item.HasReviewed);
        Assert.Empty(item.Media);
        Assert.Empty(item.UserReviewImageUrls);
    }

    [Fact(DisplayName = "UTCID16 - User review with IsEdited = null and ReviewMedia = null maps false and empty media list")]
    public async Task UTCID16_GetPurchasedPackagesAsync_ReviewWithNullIsEditedAndNullMedia_MapsSafely()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId);
        od.Package!.Reviews.Add(new Review
        {
            ReviewId = Guid.NewGuid(),
            TraineeId = traineeId,
            OrderId = od.OrderId,
            Rating = 4,
            Comment = "Good",
            IsEdited = null,
            ReviewMedia = null!
        });

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.True(item.HasReviewed);
        Assert.False(item.IsReviewEdited);
        Assert.Empty(item.UserReviewImageUrls);
    }

    [Fact(DisplayName = "UTCID17 - Empty Guid currentUserId returns empty PagedResultDto")]
    public async Task UTCID17_GetPurchasedPackagesAsync_WhenUserIdIsEmptyGuid_ReturnsEmptyList()
    {
        // Arrange
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(Guid.Empty, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail>(), 0));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(Guid.Empty, 1, 10);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact(DisplayName = "UTCID18 - Repository throws InvalidOperationException propagates exception")]
    public async Task UTCID18_GetPurchasedPackagesAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetPurchasedPackagesAsync(traineeId, 1, 10));
        Assert.Equal("Database connection failed", ex.Message);
    }

    [Fact(DisplayName = "UTCID19 - Search, filter, and sort parameters invoke filtered repository overload")]
    public async Task UTCID19_GetPurchasedPackagesAsync_WithSearchFilterAndSort_CallsFilteredRepositoryOverload()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, odPackageType: "WORKOUT", pkgPackageType: "WORKOUT");
        od.PackageTitle = "Fat Loss Bootcamp";

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(
                traineeId,
                1,
                10,
                "Fat Loss",
                "WORKOUT",
                "ACTIVE",
                "PRICE_DESC",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(
            traineeId,
            1,
            10,
            searchKeyword: "Fat Loss",
            packageType: "WORKOUT",
            status: "ACTIVE",
            sortBy: "PRICE_DESC");

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("Fat Loss Bootcamp", item.Title);
        _mockPackageRepo.Verify(r => r.GetPurchasedOrderDetailsAsync(
            traineeId,
            1,
            10,
            "Fat Loss",
            "WORKOUT",
            "ACTIVE",
            "PRICE_DESC",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "UTCID20 - Completed workout plan resolves EnrollmentStatus COMPLETED and allows review")]
    public async Task UTCID20_GetPurchasedPackagesAsync_WhenAllPlansCompleted_ResolvesEnrollmentStatusCompletedAndAllowsReview()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, odPackageType: "WORKOUT", pkgPackageType: "WORKOUT");
        od.TrainingPlans.Add(new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            OrderDetailsId = od.OrderDetailsId,
            Status = "COMPLETED",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("COMPLETED", item.EnrollmentStatus);
        Assert.True(item.CanReview);
        Assert.Null(item.CannotReviewReason);
    }

    [Fact(DisplayName = "UTCID21 - Expired order resolves EnrollmentStatus EXPIRED and RemainingDays 0")]
    public async Task UTCID21_GetPurchasedPackagesAsync_WhenExpiredOrder_ResolvesEnrollmentStatusExpiredAndZeroRemainingDays()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, odPackageType: "WORKOUT", pkgPackageType: "WORKOUT");
        od.Order.CreatedAt = DateTime.UtcNow.AddDays(-40);
        od.PackageDurationDays = 30;
        od.Package!.DurationDays = 30;

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("EXPIRED", item.EnrollmentStatus);
        Assert.Equal(0, item.RemainingDays);
    }

    [Fact(DisplayName = "UTCID22 - Rejected refund with [Policy:] prefix hides internal note and allows review when plan active")]
    public async Task UTCID22_GetPurchasedPackagesAsync_WhenRejectedRefundHasPolicyPrefix_FiltersPolicyNoteAndAllowsReviewIfPlanActive()
    {
        // Arrange
        var traineeId = Guid.NewGuid();
        var od = CreateOrderDetail(traineeId, odPackageType: "WORKOUT", pkgPackageType: "WORKOUT");
        od.Order.RefundRequests.Add(new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            OrderId = od.OrderId,
            Status = "REJECTED",
            StaffNote = "[Policy: Case 2] Internal note",
            RequestedAt = DateTime.UtcNow.AddDays(-1)
        });
        od.TrainingPlans.Add(new TrainingPlan
        {
            TrainingPlanId = Guid.NewGuid(),
            OrderDetailsId = od.OrderDetailsId,
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });

        _mockPackageRepo.Setup(r => r.GetPurchasedOrderDetailsAsync(traineeId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderDetail> { od }, 1));

        // Act
        var result = await _service.GetPurchasedPackagesAsync(traineeId, 1, 10);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("REJECTED", item.RefundStatus);
        Assert.Null(item.RefundStaffNote);
        Assert.True(item.CanReview);
    }

    #endregion
}

