using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;
using FitSocial.UnitTests.Common;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ProcessCoachPayoutServiceTests
{
    private readonly Mock<IPayoutRepository> _payouts = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ICoachBankAccountRepository> _banks = new();
    private readonly Mock<IReportRepository> _reports = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<IBankTransferGateway> _gateway = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Guid _coachId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    public static TheoryData<ProcessCoachPayoutTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ProcessCoachPayoutTestCase>();
            var assembly = typeof(ProcessCoachPayoutServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ProcessCoachPayoutTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new ProcessCoachPayoutTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    PayoutExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PayoutExists")),
                    HasUnpaid = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasUnpaid")),
                    HasRefunded = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "HasRefunded")),
                    GatewaySuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "GatewaySuccess")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }
            return data;
        }
    }

    private PayoutService CreateSut()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _reports.Setup(r => r.CountUnresolvedAgainstUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _banks.Setup(b => b.ListActiveByCoachIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<CoachBankAccount>
        {
            new() { BankId = Guid.NewGuid(), CoachId = _coachId, BankCode = "970436", AccountName = "Coach", EncryptedAccountNumber = new byte[] { 1, 2, 3 }, IsDefault = true, IsActive = true }
        });
        _encryption.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("1234567890");
        return new PayoutService(_payouts.Object, _users.Object, _banks.Object, _reports.Object, _encryption.Object, _gateway.Object, _auditLogs.Object, _notifications.Object, _unitOfWork.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ProcessCoachPayoutTest(ProcessCoachPayoutTestCase tc)
    {
        _payouts.Reset();
        _gateway.Reset();

        if (!tc.PayoutExists)
        {
            _payouts.Setup(p => p.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Payout?)null);
        }
        else
        {
            var payout = new Payout
            {
                PayoutId = Guid.NewGuid(),
                CoachId = _coachId,
                PayoutMonth = 1,
                PayoutYear = DateTime.UtcNow.Year,
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow
            };
            if (tc.HasUnpaid)
            {
                var item = new PayoutItem
                {
                    PayoutItemId = Guid.NewGuid(),
                    PayoutId = payout.PayoutId,
                    OrderId = Guid.NewGuid(),
                    GrossAmount = 100000m,
                    CommissionRate = 0.15m,
                    CommissionAmount = 15000m,
                    RefundAdjustment = 0,
                    TaxAmount = 0,
                    NetAmount = 85000m,
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                    Order = new Order { OrderStatus = tc.HasRefunded ? PaymentConstants.OrderStatusRefunded : PaymentConstants.OrderStatusPaid }
                };
                payout.PayoutItems.Add(item);
            }
            else
            {
                // already fully paid: add processed item
                payout.PayoutItems.Add(new PayoutItem
                {
                    PayoutItemId = Guid.NewGuid(),
                    PayoutId = payout.PayoutId,
                    OrderId = Guid.NewGuid(),
                    GrossAmount = 100000m,
                    NetAmount = 85000m,
                    Status = "PROCESSED",
                    CreatedAt = DateTime.UtcNow,
                    Order = new Order { OrderStatus = PaymentConstants.OrderStatusPaid }
                });
                payout.Status = "PENDING";
            }

            _payouts.Setup(p => p.GetByIdWithDetailsAsync(payout.PayoutId, It.IsAny<CancellationToken>())).ReturnsAsync(payout);

            if (tc.GatewaySuccess)
            {
                _gateway.Setup(g => g.CreateTransferAsync(It.IsAny<BankTransferRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CreatedTransfer(true, "gw-1", true, "SUCCESS", null, "TX-1"));
            }
            else
            {
                _gateway.Setup(g => g.CreateTransferAsync(It.IsAny<BankTransferRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CreatedTransfer(true, "gw-1", false, "FAILED", "fail", null));
            }

            var sut = CreateSut();
            var result = await sut.ProcessPayoutAsync(payout.PayoutId, _staffId);

            Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
                $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
            return;
        }

        // payout not found case uses any guid
        var sut2 = CreateSut();
        var result2 = await sut2.ProcessPayoutAsync(Guid.NewGuid(), _staffId);
        Assert.True(result2.Success == tc.ExpectedSuccess && result2.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result2.Success}, Message=\"{result2.Message}\"");
    }

    public class ProcessCoachPayoutTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool PayoutExists { get; set; }
        public bool HasUnpaid { get; set; }
        public bool HasRefunded { get; set; }
        public bool GatewaySuccess { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
