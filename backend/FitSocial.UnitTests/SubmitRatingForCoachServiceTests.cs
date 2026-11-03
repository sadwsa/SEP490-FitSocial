using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class SubmitRatingForCoachServiceTests
{
    private readonly Mock<ICoachProfileRepository> _coachProfiles = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<SubmitRatingForCoachTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<SubmitRatingForCoachTestCase>();
            var assembly = typeof(SubmitRatingForCoachServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.SubmitRatingForCoachTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new SubmitRatingForCoachTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    CoachExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CoachExists")),
                    OrderStatus = CsvTestHelper.Get(row, index, "OrderStatus"),
                    OrderLink = CsvTestHelper.Get(row, index, "OrderLink"),
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

    private CoachService CreateSut()
    {
        return new CoachService(
            _coachProfiles.Object,
            _locations.Object,
            _unitOfWork.Object,
            _reviews.Object,
            _orders.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task SubmitRatingForCoachTest(SubmitRatingForCoachTestCase tc)
    {
        _coachProfiles.Reset();
        _locations.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var coachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();

        if (tc.CoachExists)
        {
            _coachProfiles
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == coachId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CoachProfile { CoachId = coachId });
        }
        else
        {
            _coachProfiles
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == coachId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CoachProfile?)null);
        }

        var orders = BuildOrders(tc, coachId, traineeId, otherCoachId);
        _orders
            .Setup(r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);

        Review? capturedReview = null;
        _reviews
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .Callback<Review, CancellationToken>((review, _) => capturedReview = review)
            .Returns(Task.CompletedTask);

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
        var dto = new CreateReviewDto
        {
            Rating = tc.Rating,
            Comment = string.IsNullOrWhiteSpace(tc.Comment) ? null : tc.Comment
        };

        Func<Task> action = () => sut.SubmitReviewAsync(coachId, traineeId, dto);

        if (tc.ExpectedSuccess)
        {
            var result = await sut.SubmitReviewAsync(coachId, traineeId, dto);

            Assert.True(result.Success,
                $"{tc.UtcId} ({tc.Type}): expected success but got Success={result.Success}, Message=\"{result.Message}\"");
            Assert.Equal(tc.ExpectedMessage, result.Message);
            Assert.True(result.Data);

            Assert.NotNull(capturedReview);
            Assert.NotEqual(Guid.Empty, capturedReview!.ReviewId);
            Assert.Equal(coachId, capturedReview.CoachId);
            Assert.Equal(traineeId, capturedReview.TraineeId);
            Assert.Equal(tc.Rating, capturedReview.Rating);
            Assert.Equal(dto.Comment, capturedReview.Comment);
            Assert.NotNull(capturedReview.CreatedAt);
            Assert.True((DateTime.UtcNow - capturedReview.CreatedAt!.Value).TotalSeconds < 5,
                $"{tc.UtcId}: CreatedAt was not set to the current UTC time.");

            _coachProfiles.Verify(
                r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == coachId), It.IsAny<CancellationToken>()),
                Times.Once);
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
            return;
        }

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
        _coachProfiles.Verify(
            r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == coachId), It.IsAny<CancellationToken>()),
            Times.Once);

        if (tc.ExpectedExceptionType == "NOT_FOUND")
        {
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }
        else if (tc.ExpectedExceptionType == "VALIDATION")
        {
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }
        else
        {
            _orders.Verify(
                r => r.GetOrdersByBuyerIdAsync(traineeId, It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.NotNull(capturedReview);
        }
    }

    private static List<Order> BuildOrders(
        SubmitRatingForCoachTestCase tc,
        Guid coachId,
        Guid traineeId,
        Guid otherCoachId)
    {
        var status = ParseOrderStatus(tc.OrderStatus);
        var orders = new List<Order>();

        switch (tc.OrderLink.Trim().ToUpperInvariant())
        {
            case "DIRECT":
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    CoachId = coachId,
                    OrderStatus = status
                });
                break;

            case "PACKAGE":
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    CoachId = null,
                    OrderStatus = status,
                    OrderDetails = new List<OrderDetail>
                    {
                        new()
                        {
                            OrderDetailsId = Guid.NewGuid(),
                            Package = new TrainingPackage
                            {
                                PackageId = Guid.NewGuid(),
                                CoachId = coachId
                            }
                        }
                    }
                });
                break;

            case "OTHER":
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    CoachId = otherCoachId,
                    OrderStatus = status,
                    OrderDetails = new List<OrderDetail>
                    {
                        new()
                        {
                            OrderDetailsId = Guid.NewGuid(),
                            Package = new TrainingPackage
                            {
                                PackageId = Guid.NewGuid(),
                                CoachId = otherCoachId
                            }
                        }
                    }
                });
                break;

            case "MULTI":
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    CoachId = coachId,
                    OrderStatus = "PENDING"
                });
                orders.Add(new Order
                {
                    OrderId = Guid.NewGuid(),
                    BuyerId = traineeId,
                    CoachId = coachId,
                    OrderStatus = status
                });
                break;

            case "NONE":
                break;

            default:
                throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported order link \"{tc.OrderLink}\".");
        }

        return orders;
    }

    private static string? ParseOrderStatus(string raw)
    {
        if (string.Equals(raw, "NULL", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(raw, "EMPTY", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    public class SubmitRatingForCoachTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool CoachExists { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string OrderLink { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
