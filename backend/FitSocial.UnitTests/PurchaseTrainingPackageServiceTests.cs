using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.UnitTests.Common;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class PurchaseTrainingPackageServiceTests
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

    public static TheoryData<PurchaseTrainingPackageTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<PurchaseTrainingPackageTestCase>();
            var assembly = typeof(PurchaseTrainingPackageServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.PurchaseTrainingPackageTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new PurchaseTrainingPackageTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    BuyerIdEmpty = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "BuyerIdEmpty")),
                    PackageIdEmpty = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageIdEmpty")),
                    BuyerExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "BuyerExists")),
                    PackageExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageExists")),
                    CoachIdEqualsBuyer = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CoachIdEqualsBuyer")),
                    PackageActive = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PackageActive")),
                    AlreadyPurchased = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "AlreadyPurchased")),
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
    public async Task PurchaseTrainingPackageTest(PurchaseTrainingPackageTestCase tc)
    {
        _users.Reset();
        _packages.Reset();
        _orders.Reset();

        var buyerId = tc.BuyerIdEmpty ? Guid.Empty : Guid.NewGuid();
        var packageId = tc.PackageIdEmpty ? Guid.Empty : Guid.NewGuid();
        var coachId = tc.CoachIdEqualsBuyer ? buyerId : Guid.NewGuid();

        if (tc.BuyerExists)
        {
            _users.Setup(u => u.GetByIdAsync(buyerId, It.IsAny<CancellationToken>())).ReturnsAsync(new User { UserId = buyerId, FullName = "Buyer", Email = "buyer@example.com" });
        }
        else
        {
            _users.Setup(u => u.GetByIdAsync(buyerId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        }

        if (tc.PackageExists)
        {
            _packages.Setup(p => p.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync(new TrainingPackage
            {
                PackageId = packageId,
                CoachId = coachId,
                IsActive = tc.PackageActive,
                Title = "Package",
                DurationDays = 30,
                Price = 100000,
                Coach = null
            });
        }
        else
        {
            _packages.Setup(p => p.GetByIdAsync(packageId, It.IsAny<CancellationToken>())).ReturnsAsync((TrainingPackage?)null);
        }

        if (tc.AlreadyPurchased)
        {
            _orders.Setup(o => o.GetOrdersByBuyerIdAsync(buyerId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order>
            {
                new Order
                {
                    OrderStatus = PaymentConstants.OrderStatusPaid,
                    OrderDetails = new List<OrderDetail> { new OrderDetail { PackageId = packageId } }
                }
            });
        }
        else
        {
            _orders.Setup(o => o.GetOrdersByBuyerIdAsync(buyerId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Order>());
        }

        var sut = CreateSut();
        var result = await sut.PreparePackagePurchaseAsync(buyerId, packageId);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    public class PurchaseTrainingPackageTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool BuyerIdEmpty { get; set; }
        public bool PackageIdEmpty { get; set; }
        public bool BuyerExists { get; set; }
        public bool PackageExists { get; set; }
        public bool CoachIdEqualsBuyer { get; set; }
        public bool PackageActive { get; set; }
        public bool AlreadyPurchased { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
