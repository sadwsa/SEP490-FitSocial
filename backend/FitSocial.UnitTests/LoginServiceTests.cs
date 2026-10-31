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
                    UserExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "UserExists")),
                    PasswordHash = CsvTestHelper.Get(row, index, "PasswordHash"),
                    PasswordCorrect = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PasswordCorrect")),
                    IsLocked = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "IsLocked")),
                    WarningCount = int.TryParse(CsvTestHelper.Get(row, index, "WarningCount"), out var wc) ? wc : 0,
                    UserRole = CsvTestHelper.Get(row, index, "UserRole"),
                    CoachApproval = CsvTestHelper.Get(row, index, "CoachApproval"),
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
            _auditLogService.Object);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task LoginAccountTest(LoginTestCase tc)
    {
        _users.Reset();
        _unitOfWork.Reset();
        _coachProfiles.Reset();
        _passwordHasher.Reset();
        _tokenService.Reset();

        _passwordHasher.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(tc.PasswordCorrect);
        _tokenService.Setup(t => t.CreateSessionAsync(It.IsAny<User>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AuthResponseDto());
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        User? user = null;
        if (tc.UserExists)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = tc.Email.Trim().ToLower(),
                PasswordHash = string.Equals(tc.PasswordHash, "EMPTY", StringComparison.OrdinalIgnoreCase) ? null : "hashed",
                RoleCode = tc.UserRole,
                IsLocked = tc.IsLocked,
                WarningCount = tc.WarningCount,
                FullName = "Test User"
            };
        }
        _users.Setup(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        if (tc.UserExists && tc.IsLocked && string.Equals(tc.UserRole, "COACH", StringComparison.OrdinalIgnoreCase))
        {
            FitSocial.Domain.Entities.CoachProfile? profile = null;
            if (!string.Equals(tc.CoachApproval, "EMPTY", StringComparison.OrdinalIgnoreCase))
            {
                profile = new FitSocial.Domain.Entities.CoachProfile { ApprovalStatus = tc.CoachApproval };
            }
            _coachProfiles.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        }

        var request = new LoginRequestDto
        {
            Email = tc.Email,
            Password = tc.Password,
            RoleCode = string.IsNullOrWhiteSpace(tc.RoleCode) ? null : tc.RoleCode,
            RememberMe = tc.RememberMe
        };

        var sut = CreateSut();
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
        public bool UserExists { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public bool PasswordCorrect { get; set; }
        public bool IsLocked { get; set; }
        public int WarningCount { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public string CoachApproval { get; set; } = string.Empty;
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;

        public override string ToString() => UtcId;
    }
}
