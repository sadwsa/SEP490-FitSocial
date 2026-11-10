using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using FitSocial.UnitTests.Common;

namespace FitSocial.UnitTests;

public class LoginServiceTests
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
    private readonly Mock<ICoachUpgradeRepository> _coachUpgrades = new();
    private readonly Mock<IEkycService> _ekycService = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly Mock<IAppealService> _appealService = new();
    private readonly Mock<IAuditLogService> _auditLogService = new();

    public static TheoryData<LoginTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<LoginTestCase>();
            var assembly = typeof(LoginServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.LoginTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;

            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);

            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;

                data.Add(new LoginTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    Email = CsvTestHelper.Get(row, index, "Email"),
                    Password = CsvTestHelper.Get(row, index, "Password"),
                    RoleCode = CsvTestHelper.Get(row, index, "RoleCode"),
                    RememberMe = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "RememberMe")),
                    Result_UserExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_UserExists")),
                    Result_PasswordHash = CsvTestHelper.Get(row, index, "Result_PasswordHash"),
                    Result_PasswordCorrect = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_PasswordCorrect")),
                    Result_IsLocked = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_IsLocked")),
                    Result_WarningCount = int.TryParse(CsvTestHelper.Get(row, index, "Result_WarningCount"), out var wc) ? wc : 0,
                    Result_UserRole = CsvTestHelper.Get(row, index, "Result_UserRole"),
                    Result_CoachApproval = CsvTestHelper.Get(row, index, "Result_CoachApproval"),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage"),
                    Result_CoachUpgradesNull = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_CoachUpgradesNull")),
                    Result_LatestUpgradeStatus = CsvTestHelper.Get(row, index, "Result_LatestUpgradeStatus"),
                    Result_SessionUserPresent = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_SessionUserPresent"))
                });
            }
            return data;
        }
    }

    private AuthService CreateSut(ICoachUpgradeRepository? coachUpgrades = null)
    {
        return new AuthService(
            _users.Object,
            _unitOfWork.Object,
            _otp.Object,
            _email.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            _configuration.Object,
            _httpClientFactory.Object,
            _tokenBlacklist.Object,
            _terms.Object,
            _agreements.Object,
            _ekycs.Object,
            _certificates.Object,
            _coachProfiles.Object,
            _ekycService.Object,
            _encryptionService.Object,
            _appealService.Object,
            _auditLogService.Object,
            coachUpgrades);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task LoginAccountTest(LoginTestCase tc)
    {
        _users.Reset();
        _unitOfWork.Reset();
        _coachProfiles.Reset();
        _coachUpgrades.Reset();
        _passwordHasher.Reset();
        _tokenService.Reset();

        _passwordHasher.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(tc.Result_PasswordCorrect);
        _tokenService.Setup(t => t.CreateSessionAsync(It.IsAny<User>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AuthResponseDto());
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        User? user = null;
        if (tc.Result_UserExists)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = tc.Email.Trim().ToLower(),
                PasswordHash = string.Equals(tc.Result_PasswordHash, "EMPTY", StringComparison.OrdinalIgnoreCase) ? null : "hashed",
                RoleCode = tc.Result_UserRole == "NULL" ? null : tc.Result_UserRole,
                IsLocked = tc.Result_IsLocked,
                WarningCount = tc.Result_WarningCount,
                FullName = "Test User"
            };
        }

        var expectedNormalizedEmail = tc.Email.Trim().ToLower();
        _users.Setup(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string email, CancellationToken ct) =>
                Task.FromResult(email == expectedNormalizedEmail ? user : null));

        if (tc.Result_UserExists && tc.Result_IsLocked && string.Equals(tc.Result_UserRole, "COACH", StringComparison.OrdinalIgnoreCase))
        {
            FitSocial.Domain.Entities.CoachProfile? profile = null;
            if (string.Equals(tc.Result_CoachApproval, "NULL", StringComparison.OrdinalIgnoreCase)) { profile = new FitSocial.Domain.Entities.CoachProfile { ApprovalStatus = null }; } else if (!string.Equals(tc.Result_CoachApproval, "EMPTY", StringComparison.OrdinalIgnoreCase)) { profile = new FitSocial.Domain.Entities.CoachProfile { ApprovalStatus = tc.Result_CoachApproval }; }

            var expectedUserId = user!.UserId;
            _coachProfiles.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken ct) =>
                    Task.FromResult(id == expectedUserId ? profile : null));
        }

        if (!tc.Result_CoachUpgradesNull)
        {
            var expectedUserId = tc.Result_UserExists ? (user?.UserId ?? Guid.Empty) : Guid.Empty;
            _coachUpgrades.Setup(c => c.GetLatestFinishedByCoachIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken ct) =>
                {
                    if (id != expectedUserId)
                        return Task.FromResult<FitSocial.Domain.Entities.CoachUpgrade?>(null);

                    if (string.Equals(tc.Result_LatestUpgradeStatus, "NULL", StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<FitSocial.Domain.Entities.CoachUpgrade?>(null);

                    return Task.FromResult<FitSocial.Domain.Entities.CoachUpgrade?>(
                        new FitSocial.Domain.Entities.CoachUpgrade
                        {
                            Status = string.IsNullOrWhiteSpace(tc.Result_LatestUpgradeStatus) ? null : tc.Result_LatestUpgradeStatus
                        });
                });
        }

        var request = new LoginRequestDto
        {
            Email = tc.Email,
            Password = tc.Password,
            RoleCode = string.IsNullOrWhiteSpace(tc.RoleCode) ? null : tc.RoleCode,
            RememberMe = tc.RememberMe
        };

        var sut = CreateSut(tc.Result_CoachUpgradesNull ? null : _coachUpgrades.Object);
        var result = await sut.LoginAsync(request);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }
    public class LoginTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
        public bool Result_UserExists { get; set; }
        public string Result_PasswordHash { get; set; } = string.Empty;
        public bool Result_PasswordCorrect { get; set; }
        public bool Result_IsLocked { get; set; }
        public int Result_WarningCount { get; set; }
        public string Result_UserRole { get; set; } = string.Empty;
        public string Result_CoachApproval { get; set; } = string.Empty;
        public bool Result_CoachUpgradesNull { get; set; }
        public string Result_LatestUpgradeStatus { get; set; } = string.Empty;
        public bool Result_SessionUserPresent { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;

        public override string ToString() => UtcId;
    }
}
