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

public class CreateCoachSubscriptionPlansServiceTests
{
    private readonly Mock<ICoachSubscriptionPlanRepository> _planRepo = new();
    private readonly Mock<ICoachUpgradeRepository> _upgrades = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<SystemOperationsService>> _logger = new();

    public static TheoryData<CreateCoachSubscriptionPlansTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<CreateCoachSubscriptionPlansTestCase>();
            var assembly = typeof(CreateCoachSubscriptionPlansServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.CreateCoachSubscriptionPlansTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new CreateCoachSubscriptionPlansTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Amount = decimal.TryParse(CsvTestHelper.Get(row, index, "Amount"), out var amt) ? amt : 0m,
                    Currency = CsvTestHelper.Get(row, index, "Currency"),
                    IsActive = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "IsActive")),
                    ImageUrl = CsvTestHelper.Get(row, index, "ImageUrl"),
                    Description = CsvTestHelper.Get(row, index, "Description"),
                    SubscriptionDuration = int.TryParse(CsvTestHelper.Get(row, index, "SubscriptionDuration"), out var sd) ? (int?)sd : null,
                    TrainingPackageDuration = int.TryParse(CsvTestHelper.Get(row, index, "TrainingPackageDuration"), out var tpd) ? (int?)tpd : null,
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
    public async Task CreateCoachSubscriptionPlansTest(CreateCoachSubscriptionPlansTestCase tc)
    {
        _planRepo.Reset();
        _unitOfWork.Reset();

        _planRepo.Setup(p => p.AddAsync(It.IsAny<CoachSubscriptionPlan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        var dto = new CreateCoachSubscriptionPlanDto
        {
            Amount = tc.Amount,
            Currency = tc.Currency,
            IsActive = tc.IsActive,
            ImageUrl = string.IsNullOrWhiteSpace(tc.ImageUrl) ? null : tc.ImageUrl,
            Description = string.IsNullOrWhiteSpace(tc.Description) ? null : tc.Description,
            SubscriptionDuration = tc.SubscriptionDuration,
            TrainingPackageDuration = tc.TrainingPackageDuration
        };

        var sut = CreateSut();
        var result = await sut.CreatePlanAsync(dto);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    public class CreateCoachSubscriptionPlansTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? SubscriptionDuration { get; set; }
        public int? TrainingPackageDuration { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
