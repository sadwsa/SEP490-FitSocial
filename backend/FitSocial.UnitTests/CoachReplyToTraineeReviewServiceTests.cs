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

public class CoachReplyToTraineeReviewServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _packages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<CoachReplyToTraineeReviewTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<CoachReplyToTraineeReviewTestCase>();
            var assembly = typeof(CoachReplyToTraineeReviewServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.CoachReplyToTraineeReviewTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new CoachReplyToTraineeReviewTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PackageExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageExists")),
                    PackageOwnedByCoach = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageOwnedByCoach")),
                    ReviewExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ReviewExists")),
                    ReviewPackageMode = CsvTestHelper.Get(row, index, "ReviewPackageMode"),
                    ReviewCoachIsOther = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ReviewCoachIsOther")),
                    Reply = CsvTestHelper.Get(row, index, "Reply"),
                    ExistingReply = CsvTestHelper.Get(row, index, "ExistingReply"),
                    HasUpdatedAt = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasUpdatedAt")),
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
    public async Task CoachReplyToTraineeReviewTest(CoachReplyToTraineeReviewTestCase tc)
    {
        _packages.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var packageId = Guid.NewGuid();
        var otherPackageId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var oldUpdatedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);

        if (tc.PackageExists)
        {
            _packages
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TrainingPackage
                {
                    PackageId = packageId,
                    CoachId = tc.PackageOwnedByCoach ? coachId : otherCoachId
                });
        }
        else
        {
            _packages
                .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TrainingPackage?)null);
        }

        Review? review = null;
        if (tc.ReviewExists)
        {
            Guid? reviewPackageId = tc.ReviewPackageMode.Trim().ToUpperInvariant() switch
            {
                "MATCH" => packageId,
                "OTHER" => otherPackageId,
                "NULL" => null,
                _ => throw new InvalidOperationException(
                    $"{tc.UtcId}: unsupported review package mode \"{tc.ReviewPackageMode}\".")
            };

            review = new Review
            {
                ReviewId = reviewId,
                PackageId = reviewPackageId,
                CoachId = tc.ReviewCoachIsOther ? otherCoachId : coachId,
                TraineeId = traineeId,
                Rating = 4,
                Comment = "Great package",
                Reply = string.Equals(tc.ExistingReply, "NONE", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : tc.ExistingReply,
                CreatedAt = createdAt,
                UpdatedAt = tc.HasUpdatedAt ? oldUpdatedAt : null,
                IsEdited = false
            };
        }

        _reviews
            .Setup(r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == reviewId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(review);

        Review? updatedReview = null;
        _reviews
            .Setup(r => r.Update(It.IsAny<Review>()))
            .Callback<Review>(r => updatedReview = r);

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
        var dto = new ReplyReviewDto { Reply = tc.Reply };

        if (tc.ExpectedSuccess)
        {
            var result = await sut.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

            Assert.True(result.Success,
                $"{tc.UtcId} ({tc.Type}): expected success but got Success={result.Success}, Message=\"{result.Message}\"");
            Assert.Equal(tc.ExpectedMessage, result.Message);
            Assert.True(result.Data);

            _packages.Verify(
                r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == packageId), It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == reviewId), It.IsAny<CancellationToken>()),
                Times.Once);
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Once);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Remove(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.NotNull(review);
            Assert.NotNull(updatedReview);
            Assert.Same(review, updatedReview);
            Assert.Equal(reviewId, updatedReview!.ReviewId);
            Assert.Equal(packageId, updatedReview.PackageId);
            Assert.Equal(review.CoachId, updatedReview.CoachId);
            Assert.Equal(traineeId, updatedReview.TraineeId);
            Assert.Equal(4, updatedReview.Rating);
            Assert.Equal("Great package", updatedReview.Comment);
            Assert.Equal(tc.Reply.Trim(), updatedReview.Reply);
            Assert.Equal(createdAt, updatedReview.CreatedAt);
            Assert.NotNull(updatedReview.UpdatedAt);
            Assert.True((DateTime.UtcNow - updatedReview.UpdatedAt!.Value).TotalSeconds < 5,
                $"{tc.UtcId}: UpdatedAt was not set to the current UTC time.");
            if (tc.HasUpdatedAt)
            {
                Assert.NotEqual(oldUpdatedAt, updatedReview.UpdatedAt);
            }
            Assert.Equal(false, updatedReview.IsEdited);
            return;
        }

        Func<Task> action = () => sut.ReplyToReviewAsync(packageId, reviewId, coachId, dto);

        Exception exception;
        switch (tc.ExpectedExceptionType)
        {
            case "NOT_FOUND":
                exception = await Assert.ThrowsAsync<NotFoundException>(action);
                break;
            case "UNAUTHORIZED":
                exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
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

        if (!tc.PackageExists || tc.ExpectedExceptionType == "UNAUTHORIZED")
        {
            _reviews.Verify(
                r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == reviewId), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Never);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Remove(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
            return;
        }

        _reviews.Verify(
            r => r.GetByIdAsync(It.Is<object>(id => (Guid)id == reviewId), It.IsAny<CancellationToken>()),
            Times.Once);

        if (tc.ExpectedExceptionType == "NOT_FOUND")
        {
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Never);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Remove(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);

            if (review != null)
            {
                Assert.Equal(4, review.Rating);
                Assert.Equal("Great package", review.Comment);
                Assert.Equal(tc.HasUpdatedAt ? oldUpdatedAt : null, review.UpdatedAt);
                Assert.Equal(false, review.IsEdited);
            }
            return;
        }

        if (tc.ExpectedExceptionType == "DB_UPDATE")
        {
            _reviews.Verify(
                r => r.Update(It.IsAny<Review>()),
                Times.Once);
            _reviews.Verify(
                r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _reviews.Verify(
                r => r.Remove(It.IsAny<Review>()),
                Times.Never);
            _unitOfWork.Verify(
                u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            Assert.NotNull(review);
            Assert.NotNull(updatedReview);
            Assert.Same(review, updatedReview);
            Assert.Equal(tc.Reply.Trim(), updatedReview!.Reply);
            Assert.NotNull(updatedReview.UpdatedAt);
            return;
        }

        throw new InvalidOperationException($"{tc.UtcId}: unexpected failure path.");
    }

    public class CoachReplyToTraineeReviewTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PackageExists { get; set; }
        public bool PackageOwnedByCoach { get; set; }
        public bool ReviewExists { get; set; }
        public string ReviewPackageMode { get; set; } = string.Empty;
        public bool ReviewCoachIsOther { get; set; }
        public string Reply { get; set; } = string.Empty;
        public string ExistingReply { get; set; } = string.Empty;
        public bool HasUpdatedAt { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
