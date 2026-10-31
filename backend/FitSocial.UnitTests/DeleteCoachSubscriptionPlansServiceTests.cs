using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.UnitTests.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class DeleteCoachSubscriptionPlansServiceTests
{
    private readonly Mock<ICoachSubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<ICoachUpgradeRepository> _upgrades = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<SystemOperationsService>> _logger = new();

    public static TheoryData<DeleteCoachSubscriptionPlansTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<DeleteCoachSubscriptionPlansTestCase>();
            var assembly = typeof(DeleteCoachSubscriptionPlansServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.DeleteCoachSubscriptionPlansTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new DeleteCoachSubscriptionPlansTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PlanExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PlanExists")),
                    Upgrades = int.TryParse(CsvTestHelper.Get(row, index, "Upgrades"), out var u) ? u : 0,
                    OrderDetails = int.TryParse(CsvTestHelper.Get(row, index, "OrderDetails"), out var o) ? o : 0,
                    SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "SaveChangesThrows")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
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
    public async Task DeleteCoachSubscriptionPlansTest(DeleteCoachSubscriptionPlansTestCase tc)
    {
        _planRepo.Reset();
        _unitOfWork.Reset();

        var planId = Guid.NewGuid();
        if (tc.PlanExists)
        {
            _planRepo.Setup(p => p.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(new CoachSubscriptionPlan
            {
                CoachSubscriptionPlansId = planId,
                Amount = 100000
            });
            _planRepo.Setup(p => p.CountReferencesAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync((tc.Upgrades, tc.OrderDetails));
        }
        else
        {
            _planRepo.Setup(p => p.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync((CoachSubscriptionPlan?)null);
        }

        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        var sut = CreateSut();
        var result = await sut.DeletePlanAsync(planId);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    public class DeleteCoachSubscriptionPlansTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PlanExists { get; set; }
        public int Upgrades { get; set; }
        public int OrderDetails { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
