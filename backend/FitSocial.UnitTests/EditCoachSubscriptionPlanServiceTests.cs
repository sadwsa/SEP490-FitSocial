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

public class EditCoachSubscriptionPlanServiceTests
{
    private readonly Mock<ICoachSubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<ICoachUpgradeRepository> _upgrades = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<SystemOperationsService>> _logger = new();

    public static TheoryData<EditCoachSubscriptionPlanTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<EditCoachSubscriptionPlanTestCase>();
            var assembly = typeof(EditCoachSubscriptionPlanServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.EditCoachSubscriptionPlanTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                var amountRaw = CsvTestHelper.Get(row, index, "Amount");
                decimal? amount = null;
                if (!string.IsNullOrWhiteSpace(amountRaw)) amount = decimal.Parse(amountRaw);

                var isActiveRaw = CsvTestHelper.Get(row, index, "IsActive");
                bool? isActive = null;
                if (string.Equals(isActiveRaw, "TRUE", StringComparison.OrdinalIgnoreCase)) isActive = true;
                else if (string.Equals(isActiveRaw, "FALSE", StringComparison.OrdinalIgnoreCase)) isActive = false;

                data.Add(new EditCoachSubscriptionPlanTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PlanExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PlanExists")),
                    Amount = amount,
                    Currency = CsvTestHelper.Get(row, index, "Currency"),
                    Description = CsvTestHelper.Get(row, index, "Description"),
                    IsActive = isActive,
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
    public async Task EditCoachSubscriptionPlanTest(EditCoachSubscriptionPlanTestCase tc)
    {
        _planRepo.Reset();
        _unitOfWork.Reset();

        var planId = Guid.NewGuid();
        if (tc.PlanExists)
        {
            _planRepo.Setup(p => p.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(new CoachSubscriptionPlan
            {
                CoachSubscriptionPlansId = planId,
                Amount = 100000,
                Description = "Old",
                Currency = "VND",
                IsActive = true
            });
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

        var dto = new UpdateCoachSubscriptionPlanDto
        {
            Amount = tc.Amount,
            Currency = tc.Currency,
            Description = tc.Description,
            IsActive = tc.IsActive
        };

        var sut = CreateSut();
        var result = await sut.UpdatePlanAsync(planId, dto);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    public class EditCoachSubscriptionPlanTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PlanExists { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
