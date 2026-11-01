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
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using FitSocial.UnitTests.Common;

namespace FitSocial.UnitTests;

public class LogoutManagementPortalServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOtpService> _otp = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<ITokenBlacklistService> _tokenBlacklist = new();
    private readonly Mock<ITermsAndPolicyRepository> _terms = new();
    private readonly Mock<IUserAgreementRepository> _agreements = new();
    private readonly Mock<ICoachEkycVerificationRepository> _ekycs = new();
    private readonly Mock<ICoachCertificateRepository> _certificates = new();
    private readonly Mock<ICoachProfileRepository> _coachProfiles = new();
    private readonly Mock<IEkycService> _ekycService = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly Mock<IAppealService> _appealService = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();

    public static TheoryData<LogoutTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<LogoutTestCase>();
            var assembly = typeof(LogoutManagementPortalServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.LogoutManagementPortalTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                var expiresRaw = CsvTestHelper.Get(row, index, "ExpiresAtUtc");
                DateTime? expires = null;
                if (!string.IsNullOrWhiteSpace(expiresRaw))
                {
                    expires = DateTime.Parse(expiresRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
                }
                data.Add(new LogoutTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Jti = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "Jti")) ? null : CsvTestHelper.Get(row, index, "Jti"),
                    ExpiresAtUtc = expires,
                    RefreshToken = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "RefreshToken")) ? null : CsvTestHelper.Get(row, index, "RefreshToken"),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
                });
            }
            return data;
        }
    }

    private AuthService CreateSut()
    {
        return new AuthService(
            _users.Object, _unitOfWork.Object, _otp.Object, _email.Object, _passwordHasher.Object,
            _tokenService.Object, _configuration.Object, _httpClientFactory.Object, _tokenBlacklist.Object,
            _terms.Object, _agreements.Object, _ekycs.Object, _certificates.Object, _coachProfiles.Object,
            _ekycService.Object, _encryptionService.Object, _appealService.Object, _auditLogService.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task LogoutManagementPortalTest(LogoutTestCase tc)
    {
        _tokenBlacklist.Reset();
        _tokenService.Reset();
        _tokenBlacklist.Setup(t => t.RevokeTokenAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
        _tokenService.Setup(t => t.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut();
        var result = await sut.LogoutAsync(tc.Jti, tc.ExpiresAtUtc, tc.RefreshToken);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }
    public class LogoutTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Jti { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? RefreshToken { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
