using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.UnitTests.Common;
using FitSocial.Application.DTOs.SystemOperations;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewCoachSubscriptionPlansServiceTests
{
    private readonly Mock<ICoachSubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<ICoachUpgradeRepository> _upgrades = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<SystemOperationsService>> _logger = new();

    public static TheoryData<ViewCoachSubscriptionPlansTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewCoachSubscriptionPlansTestCase>();
            var assembly = typeof(ViewCoachSubscriptionPlansServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ViewCoachSubscriptionPlansTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                var isActiveRaw = CsvTestHelper.Get(row, index, "IsActive");
                bool? isActive = null;
                if (string.Equals(isActiveRaw, "TRUE", StringComparison.OrdinalIgnoreCase)) isActive = true;
                else if (string.Equals(isActiveRaw, "FALSE", StringComparison.OrdinalIgnoreCase)) isActive = false;

                int page = int.TryParse(CsvTestHelper.Get(row, index, "Page"), out var p) ? p : 1;
                int pageSize = int.TryParse(CsvTestHelper.Get(row, index, "PageSize"), out var ps) ? ps : 20;

                data.Add(new ViewCoachSubscriptionPlansTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    IsActive = isActive,
                    Search = CsvTestHelper.Get(row, index, "Search"),
                    Page = page,
                    PageSize = pageSize,
                    Exception = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Exception")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage"),
                    ExpectedPlanCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedPlanCount"), out var epc) ? epc : 0,
                    ExpectedTotalPlans = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedTotalPlans"), out var etp) ? etp : 0,
                    ExpectedActivePlansCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedActivePlansCount"), out var eapc) ? eapc : 0
                });
            }
            return data;
        }
    }

    private SystemOperationsService CreateSut()
    {
        return new SystemOperationsService(_planRepo.Object, _upgrades.Object, _unitOfWork.Object, _logger.Object, null);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ViewCoachSubscriptionPlansTest(ViewCoachSubscriptionPlansTestCase tc)
    {
        _planRepo.Reset();
        _upgrades.Reset();

        var plans = new List<CoachSubscriptionPlan>
        {
            new() { CoachSubscriptionPlansId = Guid.NewGuid(), Description = "Basic Plan", Amount = 100000, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { CoachSubscriptionPlansId = Guid.NewGuid(), Description = "Premium Plan", Amount = 500000, IsActive = false, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { CoachSubscriptionPlansId = Guid.NewGuid(), Description = "Pro Plan", Amount = 1000000, IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        if (tc.Exception)
        {
            _planRepo.Setup(p => p.GetAllPlansAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
        }
        else
        {
            _planRepo.Setup(p => p.GetAllPlansAsync(It.IsAny<CancellationToken>())).ReturnsAsync(plans);
        }
        _upgrades.Setup(u => u.GetCountsByPriceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, (int Total, int Active)>());

        var sut = CreateSut();
        var result = await sut.GetCoachSubscriptionPlansAsync(tc.IsActive, string.IsNullOrWhiteSpace(tc.Search) ? null : tc.Search, tc.Page, tc.PageSize);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");

        if (tc.ExpectedSuccess && result.Data != null)
        {
            Assert.Equal(tc.ExpectedPlanCount, result.Data.Plans.Count);
            Assert.Equal(tc.ExpectedTotalPlans, result.Data.TotalPlans);
            Assert.Equal(tc.ExpectedActivePlansCount, result.Data.ActivePlansCount);
        }
    }

    public class ViewCoachSubscriptionPlansTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public string Search { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public bool Exception { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public int ExpectedPlanCount { get; set; }
        public int ExpectedTotalPlans { get; set; }
        public int ExpectedActivePlansCount { get; set; }
        public override string ToString() => UtcId;
    }
}
