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
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class RenewCoachSubscriptionServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICoachSubscriptionPlanRepository> _plans = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IPaymentRepository> _payments = new();
    private readonly Mock<ICoachUpgradeRepository> _coachUpgrades = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IPaymentGateway> _paymentGateway = new();
    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<ITrainingPackageRepository> _packages = new();
    private readonly Mock<ICartRepository> _carts = new();
    private readonly Mock<ITrainingPlanRepository> _trainingPlans = new();
    private readonly Mock<INotificationRepository> _notifications = new();

    public static TheoryData<RenewCoachSubscriptionTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<RenewCoachSubscriptionTestCase>();
            var assembly = typeof(RenewCoachSubscriptionServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.RenewCoachSubscriptionTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new RenewCoachSubscriptionTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PlanExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PlanExists")),
                    PlanActive = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PlanActive")),
                    CoachExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CoachExists")),
                    SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "SaveChangesThrows")),
                    PaymentLinkThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PaymentLinkThrows")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }
            return data;
        }
    }

    private PaymentService CreateSut()
    {
        return new PaymentService(
            _users.Object, _plans.Object, _orders.Object, _payments.Object, _coachUpgrades.Object,
            _unitOfWork.Object, _tokenService.Object, _paymentGateway.Object, _authService.Object,
            _packages.Object, _carts.Object, _trainingPlans.Object, _notifications.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task RenewCoachSubscriptionTest(RenewCoachSubscriptionTestCase tc)
    {
        _plans.Reset();
        _users.Reset();
        _orders.Reset();
        _unitOfWork.Reset();
        _paymentGateway.Reset();
        _coachUpgrades.Reset();

        var planId = Guid.NewGuid();
        var coachId = Guid.NewGuid();

        if (tc.PlanExists)
        {
            _plans.Setup(p => p.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync(new CoachSubscriptionPlan
            {
                CoachSubscriptionPlansId = planId,
                IsActive = tc.PlanActive,
                Amount = 100000,
                Currency = "VND",
                Description = "Plan"
            });
        }
        else
        {
            _plans.Setup(p => p.GetByIdAsync(planId, It.IsAny<CancellationToken>())).ReturnsAsync((CoachSubscriptionPlan?)null);
        }

        if (tc.CoachExists)
        {
            _users.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserId = coachId, FullName = "Coach", Email = "coach@example.com" });
        }
        else
        {
            _users.Setup(u => u.GetByIdAsync(coachId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        }

        _orders.Setup(o => o.ListPendingActivationByUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order>());

        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("boom"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        if (tc.PaymentLinkThrows)
        {
            _paymentGateway.Setup(g => g.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("boom"));
        }
        else
        {
            _paymentGateway.Setup(g => g.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(new PaymentLinkInfo("http://checkout", 123, "qr", "acc", "name", 100000, "desc"));
        }

        _coachUpgrades.Setup(c => c.AddAsync(It.IsAny<CoachUpgrade>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _orders.Setup(o => o.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.RenewCoachSubscriptionAsync(coachId, planId, "http://localhost");

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    public class RenewCoachSubscriptionTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PlanExists { get; set; }
        public bool PlanActive { get; set; }
        public bool CoachExists { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool PaymentLinkThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
