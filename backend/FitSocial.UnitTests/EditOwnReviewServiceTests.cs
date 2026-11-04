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

public class EditOwnReviewServiceTests
{
    private readonly Mock<ICoachProfileRepository> _coachProfiles = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<EditOwnReviewTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<EditOwnReviewTestCase>();
            var assembly = typeof(EditOwnReviewServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.EditOwnReviewTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new EditOwnReviewTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    ReviewExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ReviewExists")),
                    OwnedByTrainee = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "OwnedByTrainee")),
                    Rating = int.TryParse(CsvTestHelper.Get(row, index, "Rating"), out var rating) ? rating : 0,
                    Comment = CsvTestHelper.Get(row, index, "Comment"),
                    IsEdited = CsvTestHelper.Get(row, index, "IsEdited"),
                    HasUpdatedAt = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasUpdatedAt")),
                    HasReply = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasReply")),
                    HasPackage = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasPackage")),
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
    public async Task EditOwnReviewTest(EditOwnReviewTestCase tc)
    {
        _coachProfiles.Reset();
        _locations.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var reviewId = Guid.NewGuid();
        var traineeId = Guid.NewGuid();
        var otherTraineeId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var oldUpdatedAt = new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc);
        var initialIsEdited = ParseNullableBool(tc.IsEdited);

        Review? review = null;
        if (tc.ReviewExists)
        {
            review = new Review
            {
                ReviewId = reviewId,
                TraineeId = tc.OwnedByTrainee ? traineeId : otherTraineeId,
                CoachId = coachId,
                PackageId = tc.HasPackage ? packageId : null,
                Rating = 3,
                Comment = "Old comment",
                Reply = tc.HasReply ? "Old reply" : null,
                CreatedAt = createdAt,
                UpdatedAt = tc.HasUpdatedAt ? oldUpdatedAt : null,
                IsEdited = initialIsEdited
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
        var dto = new UpdateReviewDto
        {
            Rating = tc.Rating,
            Comment = string.IsNullOrEmpty(tc.Comment) ? null : tc.Comment
        };

        if (tc.ExpectedSuccess)
        {
            var result = await sut.UpdateReviewAsync(reviewId, traineeId, dto);

            Assert.True(result.Success,
                $"{tc.UtcId} ({tc.Type}): expected success but got Success={result.Success}, Message=\"{result.Message}\"");
            Assert.Equal(tc.ExpectedMessage, result.Message);
            Assert.True(result.Data);

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
            Assert.Equal(traineeId, updatedReview.TraineeId);
            Assert.Equal(coachId, updatedReview.CoachId);
            Assert.Equal(tc.HasPackage ? packageId : null, updatedReview.PackageId);
            Assert.Equal(tc.Rating, updatedReview.Rating);
            Assert.Equal(dto.Comment, updatedReview.Comment);
            Assert.Equal(createdAt, updatedReview.CreatedAt);
            Assert.NotNull(updatedReview.UpdatedAt);
            Assert.True((DateTime.UtcNow - updatedReview.UpdatedAt!.Value).TotalSeconds < 5,
                $"{tc.UtcId}: UpdatedAt was not set to the current UTC time.");
            if (tc.HasUpdatedAt)
            {
                Assert.NotEqual(oldUpdatedAt, updatedReview.UpdatedAt);
            }
            Assert.Equal(initialIsEdited, updatedReview.IsEdited);
            Assert.Equal(tc.HasReply ? "Old reply" : null, updatedReview.Reply);
            return;
        }

        Func<Task> action = () => sut.UpdateReviewAsync(reviewId, traineeId, dto);

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
            return;
        }

        if (tc.ExpectedExceptionType == "UNAUTHORIZED")
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

            Assert.NotNull(review);
            Assert.Equal(3, review!.Rating);
            Assert.Equal("Old comment", review.Comment);
            Assert.Equal(tc.HasUpdatedAt ? oldUpdatedAt : null, review.UpdatedAt);
            Assert.Equal(initialIsEdited, review.IsEdited);
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
            Assert.Equal(tc.Rating, updatedReview!.Rating);
            Assert.Equal(dto.Comment, updatedReview.Comment);
            Assert.NotNull(updatedReview.UpdatedAt);
            return;
        }

        throw new InvalidOperationException($"{tc.UtcId}: unexpected failure path.");
    }

    private static bool? ParseNullableBool(string value)
    {
        if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase)) return null;
        return CsvTestHelper.ParseBool(value);
    }

    public class EditOwnReviewTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool ReviewExists { get; set; }
        public bool OwnedByTrainee { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string IsEdited { get; set; } = string.Empty;
        public bool HasUpdatedAt { get; set; }
        public bool HasReply { get; set; }
        public bool HasPackage { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
