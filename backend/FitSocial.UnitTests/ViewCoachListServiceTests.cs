using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using FitSocial.Infrastructure.Repositories;
using FitSocial.UnitTests.Common;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewCoachListServiceTests
{
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

                data.Add(new ViewCoachListTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    SearchKeyword = CsvTestHelper.Get(row, index, "SearchKeyword"),
                    MinExperienceRaw = CsvTestHelper.Get(row, index, "MinExperience"),
                    SortBy = CsvTestHelper.Get(row, index, "SortBy"),
                    RepositoryThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "RepositoryThrows")),
                    ExpectedCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedCount"), out var count) ? count : 0,
                    ExpectedCoachNames = CsvTestHelper.Get(row, index, "ExpectedCoachNames"),
                    ExpectedFirstStatus = CsvTestHelper.Get(row, index, "ExpectedFirstStatus"),
                    ExpectedFirstApprovalStatus = CsvTestHelper.Get(row, index, "ExpectedFirstApprovalStatus"),
                    ExpectedFirstExperienceYears = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedFirstExperienceYears"), out var exp) ? exp : 0,
                    ExpectedFirstLocationCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedFirstLocationCount"), out var loc) ? loc : 0
                });
            }

            return data;
        }
    }

    private static FitSocialDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FitSocialDbContext>()
            .UseInMemoryDatabase(databaseName: $"ViewCoachListTests_{Guid.NewGuid()}")
            .Options;

        return new FitSocialDbContext(options);
    }

    private static void SeedCoaches(FitSocialDbContext context)
    {
        var district1 = new Location
        {
            LocationId = Guid.NewGuid(),
            LocationName = "District 1 Fitness Center",
            Address = "123 Le Loi, District 1"
        };
        var thuDuc = new Location
        {
            LocationId = Guid.NewGuid(),
            LocationName = "Thu Duc Gym",
            Address = "456 Vo Van Ngan, Thu Duc"
        };

        var alice = new User
        {
            UserId = Guid.NewGuid(),
            Email = "alice@example.com",
            FullName = "Alice Johnson",
            AvatarUrl = "alice.png",
            RoleCode = "COACH"
        };
        var bob = new User
        {
            UserId = Guid.NewGuid(),
            Email = "bob@example.com",
            FullName = "Bob Smith",
            AvatarUrl = null,
            RoleCode = "COACH"
        };
        var carol = new User
        {
            UserId = Guid.NewGuid(),
            Email = "carol@example.com",
            FullName = "Carol White",
            AvatarUrl = "carol.png",
            RoleCode = "COACH"
        };

        var aliceProfile = new CoachProfile
        {
            CoachId = alice.UserId,
            Coach = alice,
            ExperienceYears = 5,
            Bio = "Certified yoga and pilates instructor",
            CertificateUrl = "alice-cert.pdf",
            Status = "ACTIVE",
            ApprovalStatus = "APPROVED"
        };
        aliceProfile.Locations.Add(district1);
        aliceProfile.Locations.Add(thuDuc);

        var bobProfile = new CoachProfile
        {
            CoachId = bob.UserId,
            Coach = bob,
            ExperienceYears = 2,
            Bio = "Strength training and weight loss coach",
            CertificateUrl = "bob-cert.pdf",
            Status = "ACTIVE",
            ApprovalStatus = "APPROVED"
        };

        var carolProfile = new CoachProfile
        {
            CoachId = carol.UserId,
            Coach = carol,
            ExperienceYears = 10,
            Bio = "Yoga master and mobility specialist",
            CertificateUrl = null,
            Status = "INACTIVE",
            ApprovalStatus = "PENDING"
        };
        carolProfile.Locations.Add(district1);

        context.Users.AddRange(alice, bob, carol);
        context.Locations.AddRange(district1, thuDuc);
        context.CoachProfiles.AddRange(aliceProfile, bobProfile, carolProfile);
    }

    private static CoachService CreateSut(ICoachProfileRepository coachProfiles)
    {
        return new CoachService(
            coachProfiles,
            Mock.Of<ILocationRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<IReviewRepository>(),
            Mock.Of<IOrderRepository>());
    }

    private static ICoachProfileRepository CreateThrowingRepository()
    {
        var mock = new Mock<ICoachProfileRepository>();
        mock.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database failure"));

        return mock.Object;
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ViewCoachListTest(ViewCoachListTestCase tc)
    {
        using var context = CreateInMemoryDbContext();
        SeedCoaches(context);
        await context.SaveChangesAsync();

        var sut = CreateSut(tc.RepositoryThrows
            ? CreateThrowingRepository()
            : new CoachProfileRepository(context));

        var searchKeyword = string.IsNullOrEmpty(tc.SearchKeyword) ? null : tc.SearchKeyword;
        var minExperience = int.TryParse(tc.MinExperienceRaw, out var exp) ? exp : (int?)null;
        var sortBy = string.IsNullOrEmpty(tc.SortBy) ? null : tc.SortBy;

        if (tc.RepositoryThrows)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.GetAllCoachesAsync(searchKeyword, minExperience, sortBy));
            Assert.Equal("Database failure", ex.Message);
            return;
        }

        var result = (await sut.GetAllCoachesAsync(searchKeyword, minExperience, sortBy)).ToList();

        Assert.Equal(tc.ExpectedCount, result.Count);

        var expectedNames = string.IsNullOrWhiteSpace(tc.ExpectedCoachNames)
            ? Array.Empty<string>()
            : tc.ExpectedCoachNames.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(expectedNames, result.Select(c => c.FullName ?? string.Empty));

        if (result.Count == 0) return;

        var first = result[0];
        Assert.Equal(tc.ExpectedFirstStatus, first.Status);
        Assert.Equal(tc.ExpectedFirstApprovalStatus, first.ApprovalStatus);
        Assert.Equal(tc.ExpectedFirstExperienceYears, first.ExperienceYears);
        Assert.Equal(tc.ExpectedFirstLocationCount, first.Locations.Count);
    }

    [Fact]
    public async Task GetAllCoachesAsync_MapsAllCoachProfileFields()
    {
        using var context = CreateInMemoryDbContext();
        SeedCoaches(context);
        await context.SaveChangesAsync();

        var sut = CreateSut(new CoachProfileRepository(context));

        var result = (await sut.GetAllCoachesAsync()).ToList();

        var alice = Assert.Single(result, c => c.FullName == "Alice Johnson");
        Assert.Equal(5, alice.ExperienceYears);
        Assert.Equal("Certified yoga and pilates instructor", alice.Bio);
        Assert.Equal("alice.png", alice.AvatarUrl);
        Assert.Equal("alice-cert.pdf", alice.CertificateUrl);
        Assert.Equal("ACTIVE", alice.Status);
        Assert.Equal("APPROVED", alice.ApprovalStatus);
        Assert.Equal(
            new[] { "District 1 Fitness Center", "Thu Duc Gym" },
            alice.Locations.OrderBy(l => l));

        var bob = Assert.Single(result, c => c.FullName == "Bob Smith");
        Assert.Null(bob.AvatarUrl);
        Assert.Empty(bob.Locations);
        Assert.Equal("bob-cert.pdf", bob.CertificateUrl);

        var carol = Assert.Single(result, c => c.FullName == "Carol White");
        Assert.Null(carol.CertificateUrl);
        Assert.Equal("INACTIVE", carol.Status);
        Assert.Equal("PENDING", carol.ApprovalStatus);
    }

    [Fact]
    public async Task GetAllCoachesAsync_WhenCoachUserIsMissing_FallsBackToUnknownCoach()
    {
        var coachProfiles = new Mock<ICoachProfileRepository>();
        coachProfiles.Setup(r => r.GetAllCoachesWithDetailsAsync(
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoachProfile>
            {
                new()
                {
                    CoachId = Guid.NewGuid(),
                    Coach = null!,
                    ExperienceYears = 3,
                    Bio = "Orphan coach profile",
                    Status = "ACTIVE",
                    ApprovalStatus = "APPROVED",
                    Locations = new List<Location>()
                }
            });

        var sut = CreateSut(coachProfiles.Object);

        var result = (await sut.GetAllCoachesAsync()).ToList();

        var coach = Assert.Single(result);
        Assert.Equal("Unknown Coach", coach.FullName);
        Assert.Null(coach.AvatarUrl);
        Assert.Empty(coach.Locations);
    }

    public class ViewCoachListTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string SearchKeyword { get; set; } = string.Empty;
        public string MinExperienceRaw { get; set; } = string.Empty;
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
