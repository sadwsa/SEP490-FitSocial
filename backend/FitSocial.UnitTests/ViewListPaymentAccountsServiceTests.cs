using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.UnitTests.Common;
using FitSocial.Application.DTOs.PaymentGateway;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewListPaymentAccountsServiceTests
{
    private readonly Mock<IPaymentGatewayConfigRepository> _configRepo = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditLogService> _auditLog = new();

    public static TheoryData<ViewListPaymentAccountsTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ViewListPaymentAccountsTestCase>();
            var assembly = typeof(ViewListPaymentAccountsServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ViewListPaymentAccountsTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new ViewListPaymentAccountsTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    ConfigCount = int.TryParse(CsvTestHelper.Get(row, index, "ConfigCount"), out var c) ? c : 0,
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage"),
                    ExpectedConfigCount = int.TryParse(CsvTestHelper.Get(row, index, "ExpectedConfigCount"), out var ec) ? ec : 0
                });
            }
            return data;
        }
    }

    private PaymentGatewayConfigService CreateSut()
    {
        return new PaymentGatewayConfigService(_configRepo.Object, _encryption.Object, _unitOfWork.Object, _auditLog.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ViewListPaymentAccountsTest(ViewListPaymentAccountsTestCase tc)
    {
        _configRepo.Reset();
        _encryption.Reset();

        var configs = new List<PaymentGatewayConfig>();
        for (int i = 0; i < tc.ConfigCount; i++)
        {
            configs.Add(new PaymentGatewayConfig
            {
                GatewayId = Guid.NewGuid(),
                GatewayName = $"Gateway {i}",
                ClientId = "client",
                EncryptedApiKey = new byte[] { 1 },
                EncryptedChecksumKey = new byte[] { 2 },
                IsActive = true,
                IsPayout = false,
                UpdatedAt = DateTime.UtcNow
            });
        }
        _configRepo.Setup(c => c.GetAllConfigsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configs);
        _encryption.Setup(e => e.Decrypt(It.IsAny<byte[]>())).Returns("secret");

        var sut = CreateSut();
        var result = await sut.GetAllConfigsAsync();

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");

        if (tc.ExpectedSuccess && result.Data != null)
        {
            Assert.Equal(tc.ExpectedConfigCount, result.Data.Count);
        }
    }

    public class ViewListPaymentAccountsTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int ConfigCount { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public int ExpectedConfigCount { get; set; }
        public override string ToString() => UtcId;
    }
}
