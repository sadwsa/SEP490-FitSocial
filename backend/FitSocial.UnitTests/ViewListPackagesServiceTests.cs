using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewListPackagesServiceTests
{
    private readonly Mock<ITrainingPackageRepository> _packages = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IOrderRepository> _orders = new();

    public static TheoryData<ViewListPackagesTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewListPackagesTestCase>();
            var assembly = typeof(ViewListPackagesServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ViewListPackagesTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);

            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new ViewListPackagesTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    SearchKeyword = CsvTestHelper.Get(row, index, "SearchKeyword"),
                    MaxPrice = CsvTestHelper.Get(row, index, "MaxPrice"),
                    CoachIdMode = CsvTestHelper.Get(row, index, "CoachIdMode"),
                    RepositoryResult = CsvTestHelper.Get(row, index, "RepositoryResult"),
                    ExpectedTitles = CsvTestHelper.Get(row, index, "ExpectedTitles"),
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
    public async Task ViewListPackagesTest(ViewListPackagesTestCase tc)
    {
        _packages.Reset();
        _unitOfWork.Reset();
        _reviews.Reset();
        _orders.Reset();

        var coachId = Guid.NewGuid();
        var otherCoachId = Guid.NewGuid();
        var searchKeyword = ParseKeyword(tc.SearchKeyword);
        var maxPrice = ParseNullableDecimal(tc.MaxPrice);
        var coachFilter = ParseCoachFilter(tc.CoachIdMode, coachId, otherCoachId);

        string? capturedKeyword = "__NOT_CALLED__";
        decimal? capturedMaxPrice = null;
        Guid? capturedCoachId = null;

        var setup = _packages
            .Setup(r => r.GetAllActivePackagesAsync(
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string?, decimal?, Guid?, CancellationToken>((keyword, price, coach, _) =>
            {
                capturedKeyword = keyword;
                capturedMaxPrice = price;
                capturedCoachId = coach;
            });

        if (string.Equals(tc.RepositoryResult, "THROWS", StringComparison.OrdinalIgnoreCase))
        {
            setup.ThrowsAsync(new InvalidOperationException(tc.ExpectedMessage));
        }
        else
        {
            setup.ReturnsAsync(BuildRepositoryResult(tc.RepositoryResult, coachId));
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
                () => sut.GetAllPackagesAsync(searchKeyword, maxPrice, coachFilter));

            Assert.Equal(tc.ExpectedMessage, exception.Message);
            VerifyRepositoryCall(searchKeyword, maxPrice, coachFilter, capturedKeyword, capturedMaxPrice, capturedCoachId);
            VerifyNoPersistenceCalls();
            return;
        }

        var result = (await sut.GetAllPackagesAsync(searchKeyword, maxPrice, coachFilter)).ToList();
        var expectedTitles = string.IsNullOrWhiteSpace(tc.ExpectedTitles)
            ? Array.Empty<string>()
            : tc.ExpectedTitles.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(expectedTitles.Length, result.Count);
        Assert.Equal(expectedTitles, result.Select(dto => dto.Title).ToArray());
        VerifyRepositoryCall(searchKeyword, maxPrice, coachFilter, capturedKeyword, capturedMaxPrice, capturedCoachId);
        VerifyNoPersistenceCalls();

        if (string.Equals(tc.RepositoryResult, "MAPPING_PACKAGE", StringComparison.OrdinalIgnoreCase))
        {
            var dto = Assert.Single(result);
            Assert.Equal("Full Coaching Plan", dto.Title);
            Assert.Equal("Complete coaching package", dto.Description);
            Assert.Equal(1500000m, dto.Price);
            Assert.Equal(30, dto.DurationDays);
            Assert.Equal((short)12, dto.SessionCount);
            Assert.Equal((short)18, dto.MinAge);
            Assert.Equal("Adults", dto.TargetAudience);
            Assert.Equal("ONLINE", dto.PackageType);
            Assert.True(dto.IsActive);
            Assert.Equal("Coach Alex", dto.CoachName);
            Assert.True(dto.CoachHasValidCertificates);
            Assert.True(dto.CoachHasExpiredCertificates);
            Assert.Equal(2, dto.Media.Count);
            Assert.Equal(new short?[] { 0, 1 }, dto.Media.Select(m => m.SortOrder).ToArray());
            Assert.Equal(new[] { "https://cdn/full-1.jpg", "https://cdn/full-2.jpg" }, dto.ImageUrls.ToArray());
            Assert.Equal("https://cdn/full-1.jpg", dto.ThumbnailUrl);
        }

        if (string.Equals(tc.RepositoryResult, "NULL_COACH_PACKAGE", StringComparison.OrdinalIgnoreCase))
        {
            var dto = Assert.Single(result);
            Assert.Equal("Unlinked Package", dto.Title);
            Assert.Equal("Unknown Coach", dto.CoachName);
            Assert.False(dto.CoachHasValidCertificates);
            Assert.False(dto.CoachHasExpiredCertificates);
            Assert.Empty(dto.Media);
            Assert.Empty(dto.ImageUrls);
            Assert.Null(dto.ThumbnailUrl);
            Assert.Equal("BOTH", dto.PackageType);
        }

        if (string.Equals(tc.RepositoryResult, "UNSORTED_MEDIA_PACKAGE", StringComparison.OrdinalIgnoreCase))
        {
            var dto = Assert.Single(result);
            Assert.Equal(new short?[] { 0, 1, 2 }, dto.Media.Select(m => m.SortOrder).ToArray());
            Assert.Equal(
                new[] { "https://cdn/first.jpg", "https://cdn/second.jpg", "https://cdn/third.jpg" },
                dto.ImageUrls.ToArray());
            Assert.Equal("https://cdn/first.jpg", dto.ThumbnailUrl);
        }
    }

    private void VerifyRepositoryCall(
        string? expectedKeyword,
        decimal? expectedMaxPrice,
        Guid? expectedCoachId,
        string? capturedKeyword,
        decimal? capturedMaxPrice,
        Guid? capturedCoachId)
    {
        _packages.Verify(
            r => r.GetAllActivePackagesAsync(
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(expectedKeyword, capturedKeyword);
        Assert.Equal(expectedMaxPrice, capturedMaxPrice);
        Assert.Equal(expectedCoachId, capturedCoachId);
    }

    private void VerifyNoPersistenceCalls()
    {
        _unitOfWork.VerifyNoOtherCalls();
        _reviews.VerifyNoOtherCalls();
        _orders.VerifyNoOtherCalls();
    }

    private static string? ParseKeyword(string value)
    {
        var token = value.Trim().ToUpperInvariant();
        return token switch
        {
            "NULL" => null,
            "EMPTY" => string.Empty,
            "SPACES" => "   ",
            _ => value
        };
    }

    private static decimal? ParseNullableDecimal(string value)
    {
        return string.Equals(value.Trim(), "NULL", StringComparison.OrdinalIgnoreCase)
            ? null
            : decimal.Parse(value.Trim(), CultureInfo.InvariantCulture);
    }

    private static Guid? ParseCoachFilter(string value, Guid coachId, Guid otherCoachId)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "NULL" => null,
            "EMPTY" => Guid.Empty,
            "MATCH" => coachId,
            "NO_MATCH" => otherCoachId,
            _ => throw new InvalidOperationException($"Unsupported coach-id mode \"{value}\".")
        };
    }

    private static List<TrainingPackage> BuildRepositoryResult(string mode, Guid coachId)
    {
        switch (mode.Trim().ToUpperInvariant())
        {
            case "TWO_PACKAGES":
                return new List<TrainingPackage>
                {
                    CreatePackage("Morning Yoga", coachId, 900000m, new DateTime(2026, 1, 3)),
                    CreatePackage("Strength Basics", coachId, 1200000m, new DateTime(2026, 1, 2))
                };

            case "ONE_PACKAGE":
                return new List<TrainingPackage>
                {
                    CreatePackage("Yoga for Beginners", coachId, 750000m, new DateTime(2026, 1, 4))
                };

            case "ORDERED_PACKAGES":
                return new List<TrainingPackage>
                {
                    CreatePackage("First Package", coachId, 100000m, new DateTime(2026, 1, 5)),
                    CreatePackage("Second Package", coachId, 200000m, new DateTime(2026, 1, 4)),
                    CreatePackage("Third Package", coachId, 300000m, new DateTime(2026, 1, 3))
                };

            case "MAPPING_PACKAGE":
                return new List<TrainingPackage> { CreateMappingPackage(coachId) };

            case "NULL_COACH_PACKAGE":
                return new List<TrainingPackage>
                {
                    new()
                    {
                        PackageId = Guid.NewGuid(),
                        CoachId = coachId,
                        Title = "Unlinked Package",
                        Description = "No coach assigned",
                        Price = 500000m,
                        DurationDays = 14,
                        SessionCount = 8,
                        MinAge = 16,
                        TargetAudience = "All levels",
                        PackageType = null,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1),
                        Coach = null!,
                        Media = null!
                    }
                };

            case "UNSORTED_MEDIA_PACKAGE":
                return new List<TrainingPackage> { CreateUnsortedMediaPackage(coachId) };

            case "EMPTY":
            case "THROWS":
                return new List<TrainingPackage>();

            default:
                throw new InvalidOperationException($"Unsupported repository result mode \"{mode}\".");
        }
    }

    private static TrainingPackage CreatePackage(string title, Guid coachId, decimal price, DateTime createdAt)
    {
        return new TrainingPackage
        {
            PackageId = Guid.NewGuid(),
            CoachId = coachId,
            Title = title,
            Description = $"{title} description",
            Price = price,
            DurationDays = 30,
            SessionCount = 12,
            MinAge = 18,
            TargetAudience = "Adults",
            PackageType = "BOTH",
            IsActive = true,
            CreatedAt = createdAt,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User
                {
                    UserId = coachId,
                    Email = "coach@example.com",
                    FullName = $"Coach {title}"
                }
            }
        };
    }

    private static TrainingPackage CreateMappingPackage(Guid coachId)
    {
        var packageId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 2, 1);
        var mediaCreatedAt = new DateTime(2026, 2, 1, 9, 0, 0);

        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Full Coaching Plan",
            Description = "Complete coaching package",
            Price = 1500000m,
            DurationDays = 30,
            SessionCount = 12,
            MinAge = 18,
            TargetAudience = "Adults",
            PackageType = "ONLINE",
            IsActive = true,
            CreatedAt = createdAt,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User
                {
                    UserId = coachId,
                    Email = "alex@example.com",
                    FullName = "Coach Alex"
                },
                CoachCertificates = new List<CoachCertificate>
                {
                    new()
                    {
                        CertificateId = Guid.NewGuid(),
                        CoachId = coachId,
                        CertificateName = "Valid Certificate",
                        ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                        VerificationStatus = "APPROVED"
                    },
                    new()
                    {
                        CertificateId = Guid.NewGuid(),
                        CoachId = coachId,
                        CertificateName = "Expired Certificate",
                        ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                        VerificationStatus = "APPROVED"
                    }
                }
            },
            Media = new List<TrainingPackageMedium>
            {
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/full-1.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 0,
                    CreatedAt = mediaCreatedAt
                },
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/full-2.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 1,
                    CreatedAt = mediaCreatedAt.AddMinutes(1)
                }
            }
        };
    }

    private static TrainingPackage CreateUnsortedMediaPackage(Guid coachId)
    {
        var packageId = Guid.NewGuid();

        return new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "Media Sort Package",
            Description = "Media should be sorted by SortOrder",
            Price = 600000m,
            DurationDays = 21,
            SessionCount = 9,
            MinAge = 18,
            TargetAudience = "Adults",
            PackageType = "BOTH",
            IsActive = true,
            CreatedAt = new DateTime(2026, 2, 2),
            Coach = null!,
            Media = new List<TrainingPackageMedium>
            {
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/third.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 2
                },
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/first.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 0
                },
                new()
                {
                    MediaId = Guid.NewGuid(),
                    PackageId = packageId,
                    MediaUrl = "https://cdn/second.jpg",
                    MediaType = "IMAGE",
                    SortOrder = 1
                }
            }
        };
    }

    public class ViewListPackagesTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SearchKeyword { get; set; } = string.Empty;
        public string MaxPrice { get; set; } = string.Empty;
        public string CoachIdMode { get; set; } = string.Empty;
        public string RepositoryResult { get; set; } = string.Empty;
        public string ExpectedTitles { get; set; } = string.Empty;
        public string ExpectedExceptionType { get; set; } = string.Empty;
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
