using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class WriteReviewForCoachServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _packages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<WriteReviewForCoachTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<WriteReviewForCoachTestCase>();
            var assembly = typeof(WriteReviewForCoachServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.WriteReviewForCoachTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new WriteReviewForCoachTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PackageExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageExists")),
                    OrderScenario = CsvTestHelper.Get(row, index, "OrderScenario"),
                    OrderStatus = CsvTestHelper.Get(row, index, "OrderStatus"),
                    ExistingReview = CsvTestHelper.Get(row, index, "ExistingReview"),
                    Rating = int.TryParse(CsvTestHelper.Get(row, index, "Rating"), out var rating) ? rating : 0,
                    Comment = CsvTestHelper.Get(row, index, "Comment"),
                    SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "SaveChangesThrows")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
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
    public async Task WriteReviewForCoachTest(WriteReviewForCoachTestCase tc)
    {
        _packages.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var packageId = Guid.NewGuid();
        var otherPackageId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var hasExistingReview = !string.Equals(tc.ExistingReview, "NONE", StringComparison.OrdinalIgnoreCase);

        if (tc.PackageExists)
        {
            _packages
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TrainingPackage
                {
                    PackageId = packageId,
                    CoachId = coachId,
                    Title = "Test package"
                });
        }
        else
        {
            _packages
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrainingPackage?)null);
        }

        var orders = BuildOrders(tc, packageId, otherPackageId, traineeId);
        _orders
            .Setup(r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        var existingReview = BuildExistingReview(
            tc.ExistingReview,
            reviewId,
            packageId,
            coachId,
            traineeId,
            createdAt,
            updatedAt);

        _reviews
            .Setup(r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReview);

        Review? addedReview = null;
        Review? updatedReview = null;

        _reviews
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .Callback<Review, CancellationToken>((review, _) => addedReview = review)
            .Returns(Task.CompletedTask);

        _reviews
            .Setup(r => r.Update(It.IsAny<Review>()))
            .Callback<Review>(review => updatedReview = review);

        if (tc.SaveChangesThrows)
        {
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("db fail"));
        }
        else
        {
            _unitOfWork
                .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
        }

        var sut = CreateSut();
        var dto = new CreatePackageReviewDto
        {
            Rating = tc.Rating,
            Comment = NormalizeComment(tc.Comment)
        };

        if (tc.ExpectedSuccess)
        {
            var result = await sut.SubmitPackageReviewAsync(packageId, traineeId, dto);

            Assert.True(result.Success,
                $"{tc.UtcId} ({tc.Type}): expected success but got Success={result.Success}, Message=\"{result.Message}\"");
            Assert.Equal(tc.ExpectedMessage, result.Message);
            Assert.True(result.Data);

            _packages.Verify(
                r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()),
                Times.Once);
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()),
                Times.Once);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            if (!hasExistingReview)
            {
                _reviews.Verify(
                    r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _reviews.Verify(
                    r => r.Update(It.IsAny<Review>()),
                    Times.Never);

                Assert.NotNull(addedReview);
                Assert.NotEqual(Guid.Empty, addedReview!.ReviewId);
                Assert.Equal(packageId, addedReview.PackageId);
                Assert.Equal(coachId, addedReview.CoachId);
                Assert.Equal(traineeId, addedReview.TraineeId);
                Assert.Equal(tc.Rating, addedReview.Rating);
                Assert.Equal(dto.Comment, addedReview.Comment);
                Assert.NotNull(addedReview.CreatedAt);
                Assert.True((DateTime.UtcNow - addedReview.CreatedAt!.Value).TotalSeconds < 5,
                    $"{tc.UtcId}: CreatedAt was not set to the current UTC time.");
                Assert.Null(addedReview.UpdatedAt);
                Assert.Equal(false, addedReview.IsEdited);
            }
            else
            {
                _reviews.Verify(
                    r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _reviews.Verify(
                    r => r.Update(It.IsAny<Review>()),
                    Times.Once);

                Assert.NotNull(existingReview);
                Assert.NotNull(updatedReview);
                Assert.Same(existingReview, updatedReview);
                Assert.Equal(reviewId, updatedReview!.ReviewId);
                Assert.Equal(packageId, updatedReview.PackageId);
                Assert.Equal(coachId, updatedReview.CoachId);
                Assert.Equal(traineeId, updatedReview.TraineeId);
                Assert.Equal(tc.Rating, updatedReview.Rating);
                Assert.Equal(dto.Comment, updatedReview.Comment);
                Assert.Equal(true, updatedReview.IsEdited);
                Assert.Equal(createdAt, updatedReview.CreatedAt);
                Assert.NotNull(updatedReview.UpdatedAt);
                Assert.True((DateTime.UtcNow - updatedReview.UpdatedAt!.Value).TotalSeconds < 5,
                    $"{tc.UtcId}: UpdatedAt was not set to the current UTC time.");
            }

            return;
        }

        Func<Task> action = () => sut.SubmitPackageReviewAsync(packageId, traineeId, dto);

        Exception exception;
        switch (tc.ExpectedExceptionType)
        {
            case "NOT_FOUND":
                exception = await Assert.ThrowsAsync<NotFoundException>(action);
                break;
            case "VALIDATION":
                exception = await Assert.ThrowsAsync<ValidationException>(action);
                break;
            case "DB_UPDATE":
                exception = await Assert.ThrowsAsync<DbUpdateException>(action);
                break;
            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported expected exception type \"{tc.ExpectedExceptionType}\".");
        }

        Assert.Equal(tc.ExpectedMessage, exception.Message);

        _packages.Verify(
            r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()),
            Times.Once);

        if (!tc.PackageExists)
        {
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
            return;
        }

        _orders.Verify(
            r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
            Times.Once);

        if (tc.ExpectedExceptionType == "VALIDATION" && !hasExistingReview)
        {
            _reviews.Verify(
                r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
            return;
        }

        _reviews.Verify(
            r => r.GetReviewByPackageAndTraineeAsync(packageId, traineeId, It.IsAny<CancellationToken>()),
            Times.Once);

        if (string.Equals(tc.ExistingReview, "ALREADY_EDITED", StringComparison.OrdinalIgnoreCase))
        {
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);

            Assert.NotNull(existingReview);
            Assert.Equal(3, existingReview!.Rating);
            Assert.Equal("Old comment", existingReview.Comment);
            Assert.Equal(true, existingReview.IsEdited);
            Assert.Equal(updatedAt, existingReview.UpdatedAt);
            return;
        }

        if (tc.ExpectedExceptionType == "DB_UPDATE")
        {
            if (!hasExistingReview)
            {
                _reviews.Verify(
                    r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                    Times.Once);
                _reviews.Verify(
                    r => r.Update(It.IsAny<Review>()),
                    Times.Never);
                Assert.NotNull(addedReview);
                Assert.Equal(tc.Rating, addedReview!.Rating);
                Assert.Equal(dto.Comment, addedReview.Comment);
            }
            else
            {
                _reviews.Verify(
                    r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                    Times.Never);
                _reviews.Verify(
                    r => r.Update(It.IsAny<Review>()),
                    Times.Once);
                Assert.NotNull(updatedReview);
            }

            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
            return;
        }

        throw new InvalidOperationException($"{tc.UtcId}: unexpected failure path.");
    }

    private static List<Order> BuildOrders(
        WriteReviewForCoachTestCase tc,
        Guid packageId,
        Guid otherPackageId,
        Guid traineeId)
    {
        var status = ParseOrderStatus(tc.OrderStatus);
        var orders = new List<Order>();

        switch (tc.OrderScenario.Trim().ToUpperInvariant())
        {
            case "MATCH":
                orders.Add(CreateOrder(traineeId, status, packageId));
                break;

            case "OTHER":
                orders.Add(CreateOrder(traineeId, status, otherPackageId));
                break;

            case "MULTI":
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    OrderStatus = "PENDING",
                    OrderDetails = new List<OrderDetail>
                    {
                        CreateOrderDetail(otherPackageId)
                    }
                });
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    OrderStatus = status,
                    OrderDetails = new List<OrderDetail>
                    {
                        CreateOrderDetail(otherPackageId),
                        CreateOrderDetail(packageId)
                    }
                });
                break;

            case "NONE":
                break;

            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported order scenario \"{tc.OrderScenario}\".");
        }

        return orders;
    }

    private static Order CreateOrder(Guid traineeId, string? status, Guid packageId)
    {
        return new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = traineeId,
            OrderStatus = status,
            OrderDetails = new List<OrderDetail>
            {
                CreateOrderDetail(packageId)
            }
        };
    }

    private static OrderDetail CreateOrderDetail(Guid packageId)
    {
        return new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            PackageId = packageId
        };
    }

    private static Review? BuildExistingReview(
        string scenario,
        Guid reviewId,
        Guid packageId,
        Guid coachId,
        Guid traineeId,
        DateTime createdAt,
        DateTime updatedAt)
    {
        if (string.Equals(scenario, "NONE", StringComparison.OrdinalIgnoreCase)) return null;

        var review = new Review
        {
            ReviewId = reviewId,
            PackageId = packageId,
            CoachId = coachId,
            TraineeId = traineeId,
            Rating = 3,
            Comment = "Old comment",
            CreatedAt = createdAt,
            UpdatedAt = null,
            IsEdited = false
        };

        switch (scenario.Trim().ToUpperInvariant())
        {
            case "NOT_EDITED":
                review.IsEdited = false;
                break;
            case "NULL_EDITED":
                review.IsEdited = null;
                break;
            case "ALREADY_EDITED":
                review.IsEdited = true;
                review.UpdatedAt = updatedAt;
                break;
            default:
                throw new InvalidOperationException($"Unsupported existing review scenario \"{scenario}\".");
        }

        return review;
    }

    private static string? ParseOrderStatus(string raw)
    {
        if (string.Equals(raw, "NULL", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(raw, "EMPTY", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static string? NormalizeComment(string comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? null : comment;
    }

    public class WriteReviewForCoachTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PackageExists { get; set; }
        public string OrderScenario { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string ExistingReview { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
