using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.UnitTests.Common;
using FitSocial.UnitTests.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ActivatePaymentAccountServiceTests
{
    private readonly Mock<IPaymentGatewayConfigRepository> _configRepo = new();
    private readonly Mock<IEncryptionService> _encryption = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditLogService> _auditLog = new();

    public static TheoryData<ActivatePaymentAccountTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ActivatePaymentAccountTestCase>();
            var assembly = typeof(ActivatePaymentAccountServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ActivatePaymentAccountTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new ActivatePaymentAccountTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Result_ConfigExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_ConfigExists")),
                    Result_OtherActiveExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_OtherActiveExists")),
                    Result_SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_SaveChangesThrows")),
                    IsPayout = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "IsPayout")),
                    Result_AuditThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_AuditThrows")),
                    ExpectedThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedThrows")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }
            return data;
        }
    }

    private PaymentGatewayConfigService CreateSut()
    {
        _auditLog.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new FitSocial.Domain.Entities.AuditLog());
        return new PaymentGatewayConfigService(_configRepo.Object, _encryption.Object, _unitOfWork.Object, _auditLog.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task ActivatePaymentAccountTest(ActivatePaymentAccountTestCase tc)
    {
        _configRepo.Reset();
        _unitOfWork.Reset();

        var id = Guid.NewGuid();
        if (tc.Result_ConfigExists)
        {
            var config = new PaymentGatewayConfig { GatewayId = id, GatewayName = "Gateway", IsActive = false, IsPayout = tc.IsPayout };
            _configRepo.Setup(c => c.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(config);

            if (tc.Result_OtherActiveExists)
            {
                var other = new PaymentGatewayConfig { GatewayId = Guid.NewGuid(), GatewayName = "Other", IsActive = true, IsPayout = tc.IsPayout };
                _configRepo.Setup(c => c.GetAllConfigsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentGatewayConfig> { config, other });
            }
            else
            {
                _configRepo.Setup(c => c.GetAllConfigsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentGatewayConfig> { config });
            }
        }
        else
        {
            _configRepo.Setup(c => c.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((PaymentGatewayConfig?)null);
        }

        if (tc.Result_SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        var sut = CreateSut();
        Exception? thrown = null;
        ApiResponseDto<bool>? result = null;
        try
        {
            result = await sut.ActivateConfigAsync(id);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        if (tc.ExpectedThrows)
        {
            Assert.NotNull(thrown);
            if (!string.IsNullOrWhiteSpace(tc.ExpectedMessage))
            {
                Assert.Contains(tc.ExpectedMessage, thrown!.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            Assert.Null(thrown);
            Assert.NotNull(result);
            Assert.True(result!.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
                $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
        }
    }

    public class ActivatePaymentAccountTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Result_ConfigExists { get; set; }
        public bool Result_OtherActiveExists { get; set; }
        public bool Result_SaveChangesThrows { get; set; }
        public bool Result_AuditThrows { get; set; }
        public bool IsPayout { get; set; }
        public bool ExpectedThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
