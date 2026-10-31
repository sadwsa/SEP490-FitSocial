using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Auth;
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

public class ChangePasswordSystemServiceTests
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

    public static TheoryData<ChangePasswordTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<ChangePasswordTestCase>();
            var assembly = typeof(ChangePasswordSystemServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.ChangePasswordSystemTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new ChangePasswordTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    CurrentPassword = CsvTestHelper.Get(row, index, "CurrentPassword"),
                    NewPassword = CsvTestHelper.Get(row, index, "NewPassword"),
                    ConfirmPassword = CsvTestHelper.Get(row, index, "ConfirmPassword"),
                    UserExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "UserExists")),
                    PasswordHash = CsvTestHelper.Get(row, index, "PasswordHash"),
                    CurrentPasswordCorrect = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CurrentPasswordCorrect")),
                    NewPasswordSameAsCurrent = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "NewPasswordSameAsCurrent")),
                    SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "SaveChangesThrows")),
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
    public async Task ChangePasswordSystemTest(ChangePasswordTestCase tc)
    {
        _users.Reset();
        _unitOfWork.Reset();
        _passwordHasher.Reset();
        _auditLogService.Reset();

        _auditLogService.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new FitSocial.Domain.Entities.AuditLog());

        _passwordHasher.Setup(p => p.VerifyPassword(tc.CurrentPassword, "hashed")).Returns(tc.CurrentPasswordCorrect);
        _passwordHasher.Setup(p => p.VerifyPassword(tc.NewPassword, "hashed")).Returns(tc.NewPasswordSameAsCurrent);
        _passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");

        User? user = null;
        if (tc.UserExists)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "user@example.com",
                PasswordHash = string.Equals(tc.PasswordHash, "EMPTY", StringComparison.OrdinalIgnoreCase) ? null : "hashed",
                FullName = "Test User",
                RoleCode = "TRAINEE"
            };
        }
        _users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db fail"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        var request = new ChangePasswordRequestDto
        {
            CurrentPassword = tc.CurrentPassword,
            NewPassword = tc.NewPassword,
            ConfirmPassword = tc.ConfirmPassword
        };

        var sut = CreateSut();
        var userId = Guid.NewGuid();
        var result = await sut.ChangePasswordAsync(userId, request);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }
    public class ChangePasswordTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public bool UserExists { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public bool CurrentPasswordCorrect { get; set; }
        public bool NewPasswordSameAsCurrent { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
