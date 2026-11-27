using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewCoachListServiceTests
{
    private readonly Mock<ICoachProfileRepository> _repo = new();

    private static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BobId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static CoachProfile MakeCoach(Guid id, string fullName, int exp, string status, string approval,
        int locationCount, params int?[] ratings)
    {
        var coach = new CoachProfile
        {
            CoachId = id,
            ExperienceYears = exp,
            Bio = "bio " + fullName,
            Status = status,
            ApprovalStatus = approval,
            Reviews = ratings.Select(r => new Review { Rating = r }).ToList(),
            Locations = Enumerable.Range(0, locationCount).Select(i => new Location { LocationName = "Loc" + i }).ToList()
        };
        coach.Coach = new User { FullName = fullName, AvatarUrl = "avatar.png" };
        return coach;
    }

    private static readonly CoachProfile Alice = MakeCoach(AliceId, "Alice Johnson", 5, "ACTIVE", "APPROVED", 2, 5, 4);
    private static readonly CoachProfile Bob = MakeCoach(BobId, "Bob Smith", 2, "ACTIVE", "APPROVED", 0, 3);

    private static readonly Dictionary<string, CoachProfile> AllByName = new()
    {
        ["Alice Johnson"] = Alice,
        ["Bob Smith"] = Bob
    };

    public static TheoryData<ViewCoachListTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewCoachListTestCase>();
            var assembly = typeof(ViewCoachListServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ViewCoachListTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                int minExp = 0;
                var minRaw = CsvTestHelper.Get(row, index, "MinExperience");
                if (!string.IsNullOrWhiteSpace(minRaw) && int.TryParse(minRaw, out var me)) minExp = me;

                bool throws = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "RepositoryThrows"));

                data.Add(new ViewCoachListTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    SearchKeyword = CsvTestHelper.Get(row, index, "SearchKeyword"),
                    MinExperience = string.IsNullOrWhiteSpace(minRaw) ? null : minExp,
                    SortBy = CsvTestHelper.Get(row, index, "SortBy"),
                    RepositoryThrows = throws,
                    ExpectedCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedCount"), out var ec) ? ec : 0,
                    ExpectedCoachNames = CsvTestHelper.Get(row, index, "ExpectedCoachNames"),
                    ExpectedFirstStatus = CsvTestHelper.Get(row, index, "ExpectedFirstStatus"),
                    ExpectedFirstApprovalStatus = CsvTestHelper.Get(row, index, "ExpectedFirstApprovalStatus"),
                    ExpectedFirstExperienceYears = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedFirstExperienceYears"), out var efe) ? efe : 0,
                    ExpectedFirstLocationCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedFirstLocationCount"), out var efl) ? efl : 0
                });
            }
            return data;
        }
    }

    private CoachService CreateSut() => new CoachService(_repo.Object, null!, null!, null!, null!);

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task GetAllCoachesAsync_ViewCoachList(ViewCoachListTestCase tc)
    {
        _repo.Reset();

        if (tc.RepositoryThrows)
        {
            _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                    It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db error"));
        }
        else
        {
            var names = string.IsNullOrWhiteSpace(tc.ExpectedCoachNames)
                ? new List<string>()
                : tc.ExpectedCoachNames.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

            var items = names.Select(n => AllByName[n]).ToList();
            var totalCount = items.Count;

            _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                    It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((items, totalCount));
        }

        var sut = CreateSut();
        var expectedKeyword = string.IsNullOrWhiteSpace(tc.SearchKeyword) ? null : tc.SearchKeyword;
        var expectedSort = string.IsNullOrWhiteSpace(tc.SortBy) ? null : tc.SortBy;

        if (tc.RepositoryThrows)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.GetAllCoachesAsync(expectedKeyword, tc.MinExperience, expectedSort, 1, 6, CancellationToken.None));

            _repo.Verify(r => r.GetAllCoachesWithDetailsAsync(
                expectedKeyword, tc.MinExperience, expectedSort, 1, 6, It.IsAny<CancellationToken>()), Times.Once);
            return;
        }

        var result = await sut.GetAllCoachesAsync(expectedKeyword, tc.MinExperience, expectedSort, 1, 6, CancellationToken.None);

        _repo.Verify(r => r.GetAllCoachesWithDetailsAsync(
            expectedKeyword, tc.MinExperience, expectedSort, 1, 6, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(tc.ExpectedCount, result.Items.Count);
        Assert.Equal(tc.ExpectedCount, result.TotalCount);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(6, result.PageSize);
        Assert.Equal(tc.ExpectedCount == 0 ? 0 : 1, result.TotalPages);
        Assert.False(result.HasPreviousPage);
        Assert.False(result.HasNextPage);

        if (tc.ExpectedCount > 0 && result.Items.Count > 0)
        {
            var first = result.Items[0];
            if (!string.IsNullOrWhiteSpace(tc.ExpectedFirstStatus))
                Assert.Equal(tc.ExpectedFirstStatus, first.Status);
            if (!string.IsNullOrWhiteSpace(tc.ExpectedFirstApprovalStatus))
                Assert.Equal(tc.ExpectedFirstApprovalStatus, first.ApprovalStatus);
            if (tc.ExpectedFirstExperienceYears > 0)
                Assert.Equal(tc.ExpectedFirstExperienceYears, first.ExperienceYears);
            Assert.Equal(tc.ExpectedFirstLocationCount, first.Locations.Count);

            var expectedNames = tc.ExpectedCoachNames.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal(expectedNames, result.Items.Select(i => i.FullName).ToList());
        }
    }

    [Fact(DisplayName = "UTCID20 - Null coach navigation maps to Unknown Coach")]
    public async Task GetAllCoachesAsync_CoachWithNullNavigation_MapsToUnknownCoach()
    {
        _repo.Reset();
        var coach = new CoachProfile
        {
            CoachId = Guid.NewGuid(),
            ExperienceYears = 3,
            Status = "ACTIVE",
            ApprovalStatus = "APPROVED",
            Reviews = new List<Review>(),
            Locations = new List<Location>(),
            Coach = null!
        };

        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { coach }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 6, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Unknown Coach", result.Items[0].FullName);
    }

    [Fact(DisplayName = "UTCID21 - Rating and total reviews are calculated from non-null ratings")]
    public async Task GetAllCoachesAsync_RatingAndTotalReviews_CalculatedFromReviews()
    {
        _repo.Reset();
        var coach = MakeCoach(Guid.NewGuid(), "Rated Coach", 4, "ACTIVE", "APPROVED", 1, 5, 4, null, 3);
        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { coach }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 6, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(4.0, result.Items[0].Rating);
        Assert.Equal(3, result.Items[0].TotalReviews);
    }

    [Fact(DisplayName = "UTCID22 - Pagination metadata passes through")]
    public async Task GetAllCoachesAsync_Pagination_PassesThrough()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { Bob }, 2));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 2, 1, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Bob Smith", result.Items[0].FullName);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID23 - Null reviews yield zero rating and zero total reviews")]
    public async Task GetAllCoachesAsync_ReviewsNull_RatingZeroAndTotalReviewsZero()
    {
        // Branch: c.Reviews is null -> null-conditional returns null -> fallback to empty list
        _repo.Reset();
        var coach = new CoachProfile
        {
            CoachId = Guid.NewGuid(),
            ExperienceYears = 3,
            Status = "ACTIVE",
            ApprovalStatus = "APPROVED",
            Reviews = null!,
            Locations = new List<Location>(),
            Coach = new User { FullName = "Null Reviews Coach", AvatarUrl = "avatar.png" }
        };

        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { coach }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 6, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("Null Reviews Coach", result.Items[0].FullName);
        Assert.Equal(0.0, result.Items[0].Rating);
        Assert.Equal(0, result.Items[0].TotalReviews);
    }

    [Fact(DisplayName = "UTCID24 - Null locations yield an empty locations list")]
    public async Task GetAllCoachesAsync_LocationsNull_EmptyLocationsList()
    {
        // Branch: c.Locations is null -> null-coalescing fallback to empty list
        _repo.Reset();
        var coach = new CoachProfile
        {
            CoachId = Guid.NewGuid(),
            ExperienceYears = 3,
            Status = "ACTIVE",
            ApprovalStatus = "APPROVED",
            Reviews = new List<Review> { new Review { Rating = 5 } },
            Locations = null!,
            Coach = new User { FullName = "Null Locations Coach", AvatarUrl = "avatar.png" }
        };

        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { coach }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 6, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(5.0, result.Items[0].Rating);
        Assert.Equal(1, result.Items[0].TotalReviews);
        Assert.Empty(result.Items[0].Locations);
    }

    [Fact(DisplayName = "UTCID25 - Non-positive pageNumber is clamped to 1")]
    public async Task GetAllCoachesAsync_NonPositivePageNumber_ClampsToOne()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { Alice }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 0, 6, CancellationToken.None);

        _repo.Verify(r => r.GetAllCoachesWithDetailsAsync(
            null, null, null, 1, 6, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(6, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID26 - Non-positive pageSize falls back to default 6")]
    public async Task GetAllCoachesAsync_NonPositivePageSize_FallsBackToDefaultSix()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { Alice }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 0, CancellationToken.None);

        _repo.Verify(r => r.GetAllCoachesWithDetailsAsync(
            null, null, null, 1, 6, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(6, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact(DisplayName = "UTCID27 - Excessive pageSize is capped at 100")]
    public async Task GetAllCoachesAsync_ExcessivePageSize_CappedAt100()
    {
        _repo.Reset();
        _repo.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachProfile> { Alice }, 1));

        var sut = CreateSut();
        var result = await sut.GetAllCoachesAsync(null, null, null, 1, 150, CancellationToken.None);

        _repo.Verify(r => r.GetAllCoachesWithDetailsAsync(
            null, null, null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    public class ViewCoachListTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SearchKeyword { get; set; } = string.Empty;
        public int? MinExperience { get; set; }
        public string SortBy { get; set; } = string.Empty;
        public bool RepositoryThrows { get; set; }
        public int ExpectedCount { get; set; }
        public string ExpectedCoachNames { get; set; } = string.Empty;
        public string ExpectedFirstStatus { get; set; } = string.Empty;
        public string ExpectedFirstApprovalStatus { get; set; } = string.Empty;
        public int ExpectedFirstExperienceYears { get; set; }
        public int ExpectedFirstLocationCount { get; set; }

        public override string ToString() => UtcId;
    }
}
