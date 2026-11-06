using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewPurchasedTrainingPackagesServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _packages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<ViewPurchasedTrainingPackagesTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewPurchasedTrainingPackagesTestCase>();
            var assembly = typeof(ViewPurchasedTrainingPackagesServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream(
                "FitSocial.UnitTests.TestData.ViewPurchasedTrainingPackagesTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                var expectedCountText = CsvTestHelper.Get(row, index, "ExpectedCount");
                data.Add(new ViewPurchasedTrainingPackagesTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    RepositoryResult = CsvTestHelper.Get(row, index, "RepositoryResult"),
                    ExpectedCount = int.TryParse(expectedCountText, out var count) ? count : 0,
                    ExpectedTitle = CsvTestHelper.Get(row, index, "ExpectedTitle"),
                    ExpectedExceptionType = CsvTestHelper.Get(row, index, "ExpectedExceptionType"),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }

            return data;
        }
    }

    private TrainingPackageService CreateSut()
    {
        return new TrainingPackageService(
            _packages.Object,
            _unitOfWork.Object,
            _reviews.Object,
            _orders.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ViewPurchasedTrainingPackagesTest(ViewPurchasedTrainingPackagesTestCase tc)
    {
        _packages.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var packages = BuildRepositoryResult(tc.RepositoryResult, userId, otherUserId, coachId);

        var setup = _packages
            .Setup(r => r.GetPurchasedPackagesAsync(userId, It.IsAny<CancellationToken>()));

        if (string.Equals(tc.RepositoryResult, "THROWS", StringComparison.OrdinalIgnoreCase))
        {
            setup.ThrowsAsync(new InvalidOperationException(tc.ExpectedMessage));
        }
        else
        {
            setup.ReturnsAsync(packages);
        }

        var sut = CreateSut();

        if (!string.Equals(tc.ExpectedExceptionType, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(tc.ExpectedExceptionType, "INVALID_OPERATION", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported expected exception type \"{tc.ExpectedExceptionType}\".");
            }

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.GetPurchasedPackagesAsync(userId));

            Assert.Equal(tc.ExpectedMessage, exception.Message);
            _packages.Verify(
                r => r.GetPurchasedPackagesAsync(userId, It.IsAny<CancellationToken>()),
                Times.Once);
            VerifyNoWriteCalls();
            return;
        }

        var result = (await sut.GetPurchasedPackagesAsync(userId)).ToList();

        Assert.Equal(tc.ExpectedCount, result.Count);
        _packages.Verify(
            r => r.GetPurchasedPackagesAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoWriteCalls();

        if (result.Count == 0)
        {
            Assert.Empty(result);
            return;
        }

        var dto = Assert.Single(result);
        var package = Assert.Single(packages);
        Assert.Equal(tc.ExpectedTitle, dto.Title);
        AssertBaseMapping(dto, package);

        switch (tc.RepositoryResult.Trim().ToUpperInvariant())
        {
            case "BASE_PACKAGE":
                AssertNoReviewOrOrderState(dto);
                Assert.Equal("ONLINE", dto.PackageType);
                break;

            case "REVIEW_NOT_EDITED":
                Assert.True(dto.HasReviewed);
                Assert.False(dto.IsReviewEdited);
                Assert.Equal(4, dto.UserRating);
                Assert.Equal("Great plan", dto.UserComment);
                Assert.Equal("ONLINE", dto.PackageType);
                Assert.Null(dto.OrderId);
                Assert.Null(dto.OrderStatus);
                break;

            case "REVIEW_EDITED":
                Assert.True(dto.HasReviewed);
                Assert.True(dto.IsReviewEdited);
                Assert.Equal(5, dto.UserRating);
                Assert.Equal("Updated review", dto.UserComment);
                Assert.Equal("ONLINE", dto.PackageType);
                Assert.Null(dto.OrderId);
                break;

            case "SINGLE_ORDER":
            {
                var expectedOrder = Assert.Single(package.OrderDetails!);
                Assert.Equal(expectedOrder.OrderDetailsId, dto.OrderDetailsId);
                Assert.Equal(expectedOrder.OrderId, dto.OrderId);
                Assert.Equal(expectedOrder.Order!.CreatedAt, dto.OrderCreatedAt);
                Assert.Equal("ACTIVE", dto.OrderStatus);
                Assert.Null(dto.RefundStatus);
                Assert.Equal("HYBRID", dto.PackageType);
                Assert.False(dto.HasReviewed);
                Assert.Null(dto.TrainingPlanId);
                Assert.Null(dto.MealPlanId);
                break;
            }

            case "MULTIPLE_ORDERS":
            {
                var expectedOrder = package.OrderDetails!
                    .Where(od => od.Order?.BuyerId == userId)
                    .OrderByDescending(od => od.CreatedAt)
                    .First();
                Assert.Equal(expectedOrder.OrderDetailsId, dto.OrderDetailsId);
                Assert.Equal(expectedOrder.OrderId, dto.OrderId);
                Assert.Equal(expectedOrder.Order!.CreatedAt, dto.OrderCreatedAt);
                Assert.Equal("ACTIVE_LATEST", dto.OrderStatus);
                Assert.Null(dto.RefundStatus);
                Assert.Equal("HYBRID", dto.PackageType);
                break;
            }

            case "LATEST_REFUND":
            {
                var expectedOrder = Assert.Single(package.OrderDetails!);
                var expectedRefund = expectedOrder.Order!.RefundRequests
                    .OrderByDescending(r => r.RequestedAt ?? r.CreatedAt)
                    .First();
                Assert.Equal(expectedOrder.OrderId, dto.OrderId);
                Assert.Equal("PAID", dto.OrderStatus);
                Assert.Equal(expectedRefund.Status, dto.RefundStatus);
                Assert.Equal("APPROVED", dto.RefundStatus);
                break;
            }

            case "LATEST_PLANS":
            {
                var expectedOrder = Assert.Single(package.OrderDetails!);
                var expectedPlan = expectedOrder.TrainingPlans
                    .OrderByDescending(p => p.CreatedAt)
                    .First();
                var expectedMealPlan = expectedOrder.MealPlans
                    .OrderByDescending(p => p.CreatedAt)
                    .First();

                Assert.Equal(expectedOrder.OrderId, dto.OrderId);
                Assert.Equal(expectedPlan.TrainingPlanId, dto.TrainingPlanId);
                Assert.Equal(expectedPlan.Status, dto.PlanStatus);
                Assert.Equal(expectedMealPlan.MealPlanId, dto.MealPlanId);
                Assert.Equal(expectedMealPlan.Status, dto.MealPlanStatus);
                Assert.Equal("ACTIVE", dto.OrderStatus);
                break;
            }

            case "OTHER_USER_ONLY":
                AssertNoReviewOrOrderState(dto);
                Assert.Equal("ONLINE", dto.PackageType);
                break;

            case "NULL_COLLECTIONS":
                AssertNoReviewOrOrderState(dto);
                Assert.Equal("ONLINE", dto.PackageType);
                break;

            case "PENDING_FALLBACK":
                Assert.Equal("REFUND_PENDING", dto.OrderStatus);
                Assert.Equal("PENDING", dto.RefundStatus);
                Assert.False(dto.HasReviewed);
                Assert.Null(dto.UserRating);
                break;

            case "REFUNDED_FALLBACK":
                Assert.Equal("REFUNDED", dto.OrderStatus);
                Assert.Equal("APPROVED", dto.RefundStatus);
                Assert.False(dto.HasReviewed);
                Assert.Null(dto.UserRating);
                break;

            case "ORDER_TYPE_NULL_PACKAGE_TYPE":
                Assert.Equal("ONLINE", dto.PackageType);
                Assert.Equal("ACTIVE", dto.OrderStatus);
                Assert.Null(dto.RefundStatus);
                break;

            case "ALL_TYPES_NULL":
                Assert.Equal("BOTH", dto.PackageType);
                Assert.Equal("ACTIVE", dto.OrderStatus);
                Assert.Null(dto.RefundStatus);
                break;

            case "MULTI_REVIEW_FILTER":
                Assert.True(dto.HasReviewed);
                Assert.False(dto.IsReviewEdited);
                Assert.Equal(5, dto.UserRating);
                Assert.Equal("Current review", dto.UserComment);
                Assert.Equal("ONLINE", dto.PackageType);
                Assert.Null(dto.OrderId);
                break;

            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported repository result mode \"{tc.RepositoryResult}\".");
        }
    }

    private void VerifyNoWriteCalls()
    {
        _unitOfWork.VerifyNoOtherCalls();
        _reviews.VerifyNoOtherCalls();
        _orders.VerifyNoOtherCalls();
    }

    private static void AssertBaseMapping(TrainingPackageResponseDto dto, TrainingPackage package)
    {
        Assert.Equal(package.PackageId, dto.PackageId);
        Assert.Equal(package.CoachId, dto.CoachId);
        Assert.Equal("Coach Minh", dto.CoachName);
        Assert.Equal(package.Title, dto.Title);
        Assert.Equal(package.Description, dto.Description);
        Assert.Equal(package.Price, dto.Price);
        Assert.Equal(package.DurationDays, dto.DurationDays);
        Assert.Equal(package.SessionCount, dto.SessionCount);
        Assert.Equal(package.MinAge, dto.MinAge);
        Assert.Equal(package.TargetAudience, dto.TargetAudience);
        Assert.Equal(package.IsActive, dto.IsActive);
        Assert.Equal(package.CreatedAt, dto.CreatedAt);
        var media = Assert.Single(dto.Media);
        Assert.Equal("https://cdn/cover.jpg", media.MediaUrl);
        Assert.Equal("https://cdn/cover.jpg", dto.ThumbnailUrl);
    }

    private static void AssertNoReviewOrOrderState(TrainingPackageResponseDto dto)
    {
        Assert.False(dto.HasReviewed);
        Assert.False(dto.IsReviewEdited);
        Assert.Null(dto.UserRating);
        Assert.Null(dto.UserComment);
        Assert.Null(dto.OrderDetailsId);
        Assert.Null(dto.OrderId);
        Assert.Null(dto.OrderCreatedAt);
        Assert.Null(dto.OrderStatus);
        Assert.Null(dto.RefundStatus);
        Assert.Null(dto.TrainingPlanId);
        Assert.Null(dto.PlanStatus);
        Assert.Null(dto.MealPlanId);
        Assert.Null(dto.MealPlanStatus);
    }

    private static List<TrainingPackage> BuildRepositoryResult(
        string mode,
        Guid userId,
        Guid otherUserId,
        Guid coachId)
    {
        switch (mode.Trim().ToUpperInvariant())
        {
            case "BASE_PACKAGE":
                return new List<TrainingPackage> { CreateBasePackage(coachId) };

            case "REVIEW_NOT_EDITED":
            {
                var package = CreateBasePackage(coachId);
                package.Reviews = new List<Review>
                {
                    CreateReview(package, coachId, userId, 4, "Great plan", false)
                };
                return new List<TrainingPackage> { package };
            }

            case "REVIEW_EDITED":
            {
                var package = CreateBasePackage(coachId);
                package.Reviews = new List<Review>
                {
                    CreateReview(package, coachId, userId, 5, "Updated review", true)
                };
                return new List<TrainingPackage> { package };
            }

            case "SINGLE_ORDER":
            {
                var package = CreateBasePackage(coachId);
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(userId, new DateTime(2026, 1, 3), "ACTIVE", "HYBRID")
                };
                return new List<TrainingPackage> { package };
            }

            case "MULTIPLE_ORDERS":
            {
                var package = CreateBasePackage(coachId);
                var olderCurrent = CreateOrderDetail(userId, new DateTime(2026, 1, 1), "OLDER", "BOTH");
                var otherBuyerNewest = CreateOrderDetail(otherUserId, new DateTime(2026, 1, 4), "OTHER", "ONLINE");
                var latestCurrent = CreateOrderDetail(userId, new DateTime(2026, 1, 3), "ACTIVE_LATEST", "HYBRID");
                package.OrderDetails = new List<OrderDetail>
                {
                    olderCurrent,
                    otherBuyerNewest,
                    latestCurrent
                };
                return new List<TrainingPackage> { package };
            }

            case "LATEST_REFUND":
            {
                var package = CreateBasePackage(coachId);
                var orderDetail = CreateOrderDetail(userId, new DateTime(2026, 1, 3), "PAID", "BOTH");
                orderDetail.Order!.RefundRequests = new List<RefundRequest>
                {
                    new()
                    {
                        RefundRequestId = Guid.NewGuid(),
                        OrderId = orderDetail.OrderId,
                        Status = "REJECTED",
                        RequestedAt = new DateTime(2026, 1, 1),
                        CreatedAt = new DateTime(2026, 1, 1)
                    },
                    new()
                    {
                        RefundRequestId = Guid.NewGuid(),
                        OrderId = orderDetail.OrderId,
                        Status = "APPROVED",
                        RequestedAt = new DateTime(2026, 1, 3),
                        CreatedAt = new DateTime(2026, 1, 2)
                    }
                };
                package.OrderDetails = new List<OrderDetail> { orderDetail };
                return new List<TrainingPackage> { package };
            }

            case "LATEST_PLANS":
            {
                var package = CreateBasePackage(coachId);
                var orderDetail = CreateOrderDetail(userId, new DateTime(2026, 1, 3), "ACTIVE", "BOTH");
                orderDetail.TrainingPlans = new List<TrainingPlan>
                {
                    new()
                    {
                        TrainingPlanId = Guid.NewGuid(),
                        CoachId = coachId,
                        OrderDetailsId = orderDetail.OrderDetailsId,
                        Title = "Older Plan",
                        Status = "DRAFT",
                        CreatedAt = new DateTime(2026, 1, 1)
                    },
                    new()
                    {
                        TrainingPlanId = Guid.NewGuid(),
                        CoachId = coachId,
                        OrderDetailsId = orderDetail.OrderDetailsId,
                        Title = "Latest Plan",
                        Status = "PUBLISHED",
                        CreatedAt = new DateTime(2026, 1, 3)
                    }
                };
                orderDetail.MealPlans = new List<MealPlan>
                {
                    new()
                    {
                        MealPlanId = Guid.NewGuid(),
                        CoachId = coachId,
                        OrderDetailsId = orderDetail.OrderDetailsId,
                        Title = "Older Meal Plan",
                        Status = "DRAFT",
                        CreatedAt = new DateTime(2026, 1, 1)
                    },
                    new()
                    {
                        MealPlanId = Guid.NewGuid(),
                        CoachId = coachId,
                        OrderDetailsId = orderDetail.OrderDetailsId,
                        Title = "Latest Meal Plan",
                        Status = "PUBLISHED",
                        CreatedAt = new DateTime(2026, 1, 3)
                    }
                };
                package.OrderDetails = new List<OrderDetail> { orderDetail };
                return new List<TrainingPackage> { package };
            }

            case "OTHER_USER_ONLY":
            {
                var package = CreateBasePackage(coachId);
                package.Reviews = new List<Review>
                {
                    CreateReview(package, coachId, otherUserId, 1, "Other trainee review", false)
                };
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(otherUserId, new DateTime(2026, 1, 5), "ACTIVE", "ONLINE")
                };
                return new List<TrainingPackage> { package };
            }

            case "NULL_COLLECTIONS":
            {
                var package = CreateBasePackage(coachId);
                package.Reviews = null!;
                package.OrderDetails = null!;
                return new List<TrainingPackage> { package };
            }

            case "PENDING_FALLBACK":
            {
                var package = CreateBasePackage(coachId);
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(userId, new DateTime(2026, 1, 3), "REFUND_PENDING", "BOTH")
                };
                return new List<TrainingPackage> { package };
            }

            case "REFUNDED_FALLBACK":
            {
                var package = CreateBasePackage(coachId);
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(userId, new DateTime(2026, 1, 3), "REFUNDED", "BOTH")
                };
                return new List<TrainingPackage> { package };
            }

            case "ORDER_TYPE_NULL_PACKAGE_TYPE":
            {
                var package = CreateBasePackage(coachId);
                package.PackageType = "ONLINE";
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(userId, new DateTime(2026, 1, 3), "ACTIVE", null)
                };
                return new List<TrainingPackage> { package };
            }

            case "ALL_TYPES_NULL":
            {
                var package = CreateBasePackage(coachId);
                package.PackageType = null;
                package.OrderDetails = new List<OrderDetail>
                {
                    CreateOrderDetail(userId, new DateTime(2026, 1, 3), "ACTIVE", null)
                };
                return new List<TrainingPackage> { package };
            }

            case "MULTI_REVIEW_FILTER":
            {
                var package = CreateBasePackage(coachId);
                package.Reviews = new List<Review>
                {
                    CreateReview(package, coachId, otherUserId, 1, "Other trainee review", false),
                    CreateReview(package, coachId, userId, 5, "Current review", false)
                };
                return new List<TrainingPackage> { package };
            }

            case "EMPTY":
            case "THROWS":
                return new List<TrainingPackage>();

            default:
                throw new InvalidOperationException($"Unsupported repository result mode \"{mode}\".");
        }
    }

    private static TrainingPackage CreateBasePackage(Guid coachId)
    {
        var packageId = Guid.NewGuid();

        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Premium Coaching",
            Description = "Personalized training package",
            Price = 1200000m,
            DurationDays = 30,
            SessionCount = 12,
            MinAge = 18,
            TargetAudience = "Adults",
            PackageType = "ONLINE",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1),
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User
                {
                    UserId = coachId,
                    Email = "coach.minh@example.com",
                    FullName = "Coach Minh"
                }
            },
            Media = new List<TrainingPackageMedium>
            {
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/cover.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 0,
                    CreatedAt = new DateTime(2026, 1, 1)
                }
            },
            Reviews = new List<Review>(),
            OrderDetails = new List<OrderDetail>()
        };
    }

    private static Review CreateReview(
        TrainingPackage package,
        Guid coachId,
        Guid traineeId,
        int rating,
        string comment,
        bool isEdited)
    {
        return new Review
        {
            ReviewId = Guid.NewGuid(),
            PackageId = package.PackageId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = rating,
            Comment = comment,
            IsEdited = isEdited,
            CreatedAt = new DateTime(2026, 1, 2)
        };
    }

    private static OrderDetail CreateOrderDetail(
        Guid buyerId,
        DateTime createdAt,
        string status,
        string? packageType)
    {
        var orderId = Guid.NewGuid();

        return new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = orderId,
            PackageId = Guid.NewGuid(),
            PackageType = packageType,
            CreatedAt = createdAt,
            Order = new Order
            {
                OrderId = orderId,
                BuyerId = buyerId,
                OrderStatus = status,
                CreatedAt = createdAt.AddHours(-2),
                RefundRequests = new List<RefundRequest>()
            }
        };
    }

    public class ViewPurchasedTrainingPackagesTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string RepositoryResult { get; set; } = string.Empty;
        public int ExpectedCount { get; set; }
        public string ExpectedTitle { get; set; } = string.Empty;
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
