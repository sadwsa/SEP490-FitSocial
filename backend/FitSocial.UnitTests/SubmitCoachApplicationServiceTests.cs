using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Coach;
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

public class SubmitCoachApplicationServiceTests
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

    public static TheoryData<SubmitCoachApplicationTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<SubmitCoachApplicationTestCase>();
            var assembly = typeof(SubmitCoachApplicationServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.SubmitCoachApplicationTestCases.csv");
            if (stream == null) return data;
            using var reader = new StreamReader(stream);
            var rows = CsvTestHelper.ParseCsv(reader.ReadToEnd());
            if (rows.Count <= 1) return data;
            var header = rows[0];
            var index = header.Select((name, i) => (name.Trim(), i)).ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var row in rows.Skip(1))
            {
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                data.Add(new SubmitCoachApplicationTestCase
                {
                    UtcId = CsvTestHelper.Get(row, index, "UTCID"),
                    Type = CsvTestHelper.Get(row, index, "Type"),
                    FullName = CsvTestHelper.Get(row, index, "FullName"),
                    Email = CsvTestHelper.Get(row, index, "Email"),
                    Password = CsvTestHelper.Get(row, index, "Password"),
                    ConfirmPassword = CsvTestHelper.Get(row, index, "ConfirmPassword"),
                    OtpCode = CsvTestHelper.Get(row, index, "OtpCode"),
                    PhoneRaw = CsvTestHelper.Get(row, index, "PhoneNumber"),
                    FrontCardUrl = CsvTestHelper.Get(row, index, "FrontCardUrl"),
                    BackCardUrl = CsvTestHelper.Get(row, index, "BackCardUrl"),
                    FaceImageUrl = CsvTestHelper.Get(row, index, "FaceImageUrl"),
                    TermRaw = CsvTestHelper.Get(row, index, "TermId"),
                    ExperienceYears = int.TryParse(CsvTestHelper.Get(row, index, "ExperienceYears"), out var ey) ? ey : 0,
                    EmailExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "EmailExists")),
                    ExistingLocked = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExistingLocked")),
                    TermExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "TermExists")),
                    PhoneExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PhoneExists")),
                    OtpValid = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "OtpValid")),
                    OtpMessage = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "OtpMessage")) ? "Invalid or expired OTP code." : CsvTestHelper.Get(row, index, "OtpMessage"),
                    EkycValid = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "EkycValid")),
                    EkycError = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "EkycError")) ? "ID verification failed." : CsvTestHelper.Get(row, index, "EkycError"),
                    IdCardDuplicate = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "IdCardDuplicate")),
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
    public async Task SubmitCoachApplicationTest(SubmitCoachApplicationTestCase tc)
    {
        _users.Reset();
        _unitOfWork.Reset();
        _otp.Reset();
        _terms.Reset();
        _agreements.Reset();
        _ekycs.Reset();
        _certificates.Reset();
        _coachProfiles.Reset();
        _ekycService.Reset();
        _encryptionService.Reset();
        _passwordHasher.Reset();

        _passwordHasher.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed");
        _encryptionService.Setup(e => e.Encrypt(It.IsAny<string>(), It.IsAny<bool>())).Returns(new byte[] { 1, 2, 3 });
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _agreements.Setup(a => a.AddAsync(It.IsAny<UserAgreement>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ekycs.Setup(e => e.AddAsync(It.IsAny<CoachEkycVerification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _certificates.Setup(c => c.AddAsync(It.IsAny<CoachCertificate>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _coachProfiles.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((CoachProfile?)null);
        _coachProfiles.Setup(c => c.AddAsync(It.IsAny<CoachProfile>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        Guid? termId = ParseTermId(tc.TermRaw);

        User? existing = null;
        if (tc.EmailExists)
        {
            existing = new User
            {
                UserId = Guid.NewGuid(),
                Email = tc.Email.Trim().ToLower(),
                IsLocked = tc.ExistingLocked
            };
        }
        _users.Setup(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            if (tc.TermExists)
            {
                _terms.Setup(t => t.GetByIdAsync(termId.Value, It.IsAny<CancellationToken>())).ReturnsAsync(new TermsAndPolicy { TermId = termId.Value });
            }
            else
            {
                _terms.Setup(t => t.GetByIdAsync(termId.Value, It.IsAny<CancellationToken>())).ReturnsAsync((TermsAndPolicy?)null);
            }
        }

        var phone = string.IsNullOrWhiteSpace(tc.PhoneRaw) ? null : tc.PhoneRaw.Trim();
        if (phone != null)
        {
            _users.Setup(u => u.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(tc.PhoneExists);
        }

        _otp.Setup(o => o.ValidateOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((tc.OtpValid, tc.OtpValid ? "OTP verified." : tc.OtpMessage));

        var ekycExtract = new EkycExtract("123456789", "Coach One", null, tc.EkycValid ? "SUCCESS" : "FAILED", tc.EkycValid ? null : tc.EkycError);
        _ekycService.Setup(e => e.VerifyAsync(It.IsAny<EkycVerificationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((tc.EkycValid, tc.EkycValid ? null : (string?)tc.EkycError, ekycExtract));

        _ekycs.Setup(e => e.ExistsByIdCardNumberAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(tc.IdCardDuplicate);

        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db fail"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        var request = new RegisterCoachRequestDto
        {
            FullName = tc.FullName,
            Email = tc.Email,
            Password = tc.Password,
            ConfirmPassword = tc.ConfirmPassword,
            OtpCode = tc.OtpCode,
            PhoneNumber = tc.PhoneRaw,
            FrontCardUrl = string.IsNullOrWhiteSpace(tc.FrontCardUrl) ? null : tc.FrontCardUrl,
            BackCardUrl = string.IsNullOrWhiteSpace(tc.BackCardUrl) ? null : tc.BackCardUrl,
            FaceImageUrl = string.IsNullOrWhiteSpace(tc.FaceImageUrl) ? null : tc.FaceImageUrl,
            TermId = termId,
            ExperienceYears = tc.ExperienceYears
        };

        var sut = CreateSut();
        var result = await sut.RegisterCoachAsync(request);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    private static Guid? ParseTermId(string value)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v) || string.Equals(v, "EMPTY", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(v, "EMPTYGUID", StringComparison.OrdinalIgnoreCase)) return Guid.Empty;
        return Guid.Parse(v);
    }
    public class SubmitCoachApplicationTestCase
    {
        public string UtcId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public string PhoneRaw { get; set; } = string.Empty;
        public string FrontCardUrl { get; set; } = string.Empty;
        public string BackCardUrl { get; set; } = string.Empty;
        public string FaceImageUrl { get; set; } = string.Empty;
        public string TermRaw { get; set; } = string.Empty;
        public int ExperienceYears { get; set; }
        public bool EmailExists { get; set; }
        public bool ExistingLocked { get; set; }
        public bool TermExists { get; set; }
        public bool PhoneExists { get; set; }
        public bool OtpValid { get; set; }
        public string OtpMessage { get; set; } = string.Empty;
        public bool EkycValid { get; set; }
        public string EkycError { get; set; } = string.Empty;
        public bool IdCardDuplicate { get; set; }
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
