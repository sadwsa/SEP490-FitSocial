using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Coach;
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
    private readonly Mock<ICoachUpgradeRepository> _coachUpgrades = new();
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
            var csvText = reader.ReadToEnd();

            var rows = CsvTestHelper.ParseCsv(csvText);
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
                    GenderRaw = CsvTestHelper.Get(row, index, "Gender"),
                    DobRaw = CsvTestHelper.Get(row, index, "DateOfBirth"),
                    TermRaw = CsvTestHelper.Get(row, index, "TermId"),
                    FrontCardUrl = CsvTestHelper.Get(row, index, "FrontCardUrl"),
                    BackCardUrl = CsvTestHelper.Get(row, index, "BackCardUrl"),
                    FaceImageUrl = CsvTestHelper.Get(row, index, "FaceImageUrl"),
                    ExperienceYearsRaw = CsvTestHelper.Get(row, index, "ExperienceYears"),
                    Biography = CsvTestHelper.Get(row, index, "Biography"),
                    CertificateUrl = CsvTestHelper.Get(row, index, "CertificateUrl"),
                    CertificatesProvided = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CertificatesProvided")),
                    CertificateUrlProvided = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CertificateUrlProvided")),
                    CertificateNameProvided = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "CertificateNameProvided")),
                    Result_ExistingUser = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_ExistingUser")),
                    Result_ExistingIsLocked = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_ExistingIsLocked")),
                    Result_CoachUpgradesNull = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_CoachUpgradesNull")),
                    Result_LatestUpgradeStatus = CsvTestHelper.Get(row, index, "Result_LatestUpgradeStatus"),
                    Result_TermExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_TermExists")),
                    Result_PhoneExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_PhoneExists")),
                    Result_OtpValid = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_OtpValid")),
                    Result_OtpMessage = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "Result_OtpMessage")) ? "Invalid or expired OTP code." : CsvTestHelper.Get(row, index, "Result_OtpMessage"),
                    Result_EkycSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_EkycSuccess")),
                    Result_EkycError = CsvTestHelper.Get(row, index, "Result_EkycError"),
                    Result_IdCardExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_IdCardExists")),
                    Result_AgreementExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_AgreementExists")),
                    Result_SaveChangesThrows = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "Result_SaveChangesThrows")),
                    ExpectedSuccess = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "ExpectedSuccess")),
                    ExpectedMessage = CsvTestHelper.Get(row, index, "ExpectedMessage")
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
    public async Task SubmitCoachApplicationAccountTest(SubmitCoachApplicationTestCase tc)
    {
        _users.Reset();
        _unitOfWork.Reset();
        _otp.Reset();
        _terms.Reset();
        _agreements.Reset();
        _passwordHasher.Reset();
        _tokenService.Reset();
        _coachProfiles.Reset();
        _coachUpgrades.Reset();
        _ekycs.Reset();
        _certificates.Reset();
        _ekycService.Reset();
        _encryptionService.Reset();

        _passwordHasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed");
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _ekycs.Setup(e => e.AddAsync(It.IsAny<CoachEkycVerification>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _certificates.Setup(c => c.AddAsync(It.IsAny<CoachCertificate>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _agreements.Setup(a => a.AddAsync(It.IsAny<UserAgreement>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _encryptionService.Setup(e => e.Encrypt(It.IsAny<string>(), It.IsAny<bool>())).Returns(new byte[] { 1, 2, 3 });

        var expectedNormalizedEmail = tc.Email.Trim().ToLower();
        User? existingUser = null;
        if (tc.Result_ExistingUser)
        {
            existingUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = expectedNormalizedEmail,
                IsLocked = tc.Result_ExistingIsLocked
            };
        }

        _users.Setup(u => u.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string email, CancellationToken ct) =>
                Task.FromResult(email == expectedNormalizedEmail ? existingUser : null));

        if (!tc.Result_CoachUpgradesNull && existingUser != null && tc.Result_ExistingIsLocked)
        {
            var expectedUserId = existingUser.UserId;
            _coachUpgrades.Setup(c => c.GetLatestFinishedByCoachIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, CancellationToken ct) =>
                {
                    if (id != expectedUserId)
                        return Task.FromResult<CoachUpgrade?>(null);

                    if (string.Equals(tc.Result_LatestUpgradeStatus, "NULL", StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult<CoachUpgrade?>(null);

                    return Task.FromResult<CoachUpgrade?>(
                        new CoachUpgrade
                        {
                            Status = string.IsNullOrWhiteSpace(tc.Result_LatestUpgradeStatus) ? null : tc.Result_LatestUpgradeStatus
                        });
                });
        }

        var phone = (tc.PhoneRaw == "NULL" || string.IsNullOrWhiteSpace(tc.PhoneRaw)) ? null : tc.PhoneRaw.Trim();
        if (phone != null)
        {
            var expectedNormalizedPhone = tc.PhoneRaw.Trim();
            _users.Setup(u => u.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .Returns((string phoneNumber, Guid? excludingUserId, CancellationToken ct) =>
                    Task.FromResult(
                        phoneNumber == expectedNormalizedPhone
                            ? tc.Result_PhoneExists
                            : false
                    ));
        }

        Guid? termId = ParseTermId(tc.TermRaw);
        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            var expectedTermId = termId.Value;
            if (tc.Result_TermExists)
            {
                _terms.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns((Guid id, CancellationToken ct) =>
                        Task.FromResult<TermsAndPolicy?>(
                            id == expectedTermId
                                ? new TermsAndPolicy { TermId = id }
                                : null
                        ));
            }
            else
            {
                _terms.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .Returns((Guid id, CancellationToken ct) =>
                        Task.FromResult<TermsAndPolicy?>(
                            id == expectedTermId
                                ? null
                                : new TermsAndPolicy { TermId = id }
                        ));
            }
        }

        var expectedTrimmedOtp = (tc.OtpCode ?? "").Trim();
        _otp.Setup(o => o.ValidateOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string email, string otpCode, string purpose) =>
                Task.FromResult(
                    otpCode == expectedTrimmedOtp
                        ? (tc.Result_OtpValid, tc.Result_OtpValid ? "OTP verified." : tc.Result_OtpMessage)
                        : (false, "Invalid or expired OTP code.")
                ));

        _ekycService.Setup(e => e.VerifyAsync(It.IsAny<EkycVerificationDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                tc.Result_EkycSuccess,
                tc.Result_EkycSuccess ? null : (string.IsNullOrWhiteSpace(tc.Result_EkycError) ? "ID verification failed." : tc.Result_EkycError),
                new EkycExtract("ID123", "Test Name", null, tc.Result_EkycSuccess ? "SUCCESS" : "FAILED", null)
            ));

        _ekycs.Setup(e => e.ExistsByIdCardNumberAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((string idCardNumber, Guid? excludingCoachId, CancellationToken ct) =>
                Task.FromResult(idCardNumber == "ID123" ? tc.Result_IdCardExists : false));

        _agreements.Setup(a => a.FindByUserAndTermAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid userId, Guid termId2, CancellationToken ct) =>
                Task.FromResult(tc.Result_AgreementExists ? new UserAgreement { AgreementId = Guid.NewGuid(), UserId = userId, TermId = termId2 } : null));

        if (tc.Result_SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db fail"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(tc.DobRaw) && !string.Equals(tc.DobRaw, "EMPTY", StringComparison.OrdinalIgnoreCase) && !string.Equals(tc.DobRaw, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            dob = DateOnly.Parse(tc.DobRaw);
        }

        int? experienceYears = null;
        if (!string.IsNullOrWhiteSpace(tc.ExperienceYearsRaw) && int.TryParse(tc.ExperienceYearsRaw, out var ey))
        {
            experienceYears = ey;
        }

        List<CoachCertificateDto>? certificates = null;
        if (tc.CertificatesProvided)
        {
            certificates = new List<CoachCertificateDto>
            {
                new CoachCertificateDto
                {
                    CertificateUrl = tc.CertificateUrlProvided ? "cert-url" : "   ",
                    CertificateName = tc.CertificateNameProvided ? "Cert" : "   "
                }
            };
        }

        var request = new RegisterCoachRequestDto
        {
            FullName = tc.FullName,
            Email = tc.Email,
            Password = tc.Password,
            ConfirmPassword = tc.ConfirmPassword,
            OtpCode = tc.OtpCode,
            PhoneNumber = phone,
            Gender = (tc.GenderRaw == "NULL" || string.IsNullOrWhiteSpace(tc.GenderRaw)) ? null : tc.GenderRaw,
            DateOfBirth = dob,
            TermId = termId,
            FrontCardUrl = tc.FrontCardUrl,
            BackCardUrl = tc.BackCardUrl,
            FaceImageUrl = tc.FaceImageUrl,
            ExperienceYears = experienceYears,
            Biography = string.IsNullOrWhiteSpace(tc.Biography) ? null : tc.Biography,
            CertificateUrl = tc.CertificateUrl,
            Certificates = certificates
        };

        var sut = CreateSut(tc.Result_CoachUpgradesNull ? null : _coachUpgrades.Object);
        var result = await sut.RegisterCoachAsync(request, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal(tc.ExpectedSuccess, result!.Success);
        Assert.Equal(tc.ExpectedMessage, result.Message);
        if (tc.ExpectedSuccess)
        {
            Assert.NotNull(result.Data);
            Assert.IsType<User>(result.Data);
        }
        else
        {
            Assert.Null(result.Data);
        }
    }

    private static Guid? ParseTermId(string value)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v) || string.Equals(v, "EMPTY", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "NULL", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(v, "EMPTYGUID", StringComparison.OrdinalIgnoreCase))
            return Guid.Empty;
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
        public string GenderRaw { get; set; } = string.Empty;
        public string DobRaw { get; set; } = string.Empty;
        public string TermRaw { get; set; } = string.Empty;
        public string FrontCardUrl { get; set; } = string.Empty;
        public string BackCardUrl { get; set; } = string.Empty;
        public string FaceImageUrl { get; set; } = string.Empty;
        public string ExperienceYearsRaw { get; set; } = string.Empty;
        public string Biography { get; set; } = string.Empty;
        public string CertificateUrl { get; set; } = string.Empty;
        public bool CertificatesProvided { get; set; }
        public bool CertificateUrlProvided { get; set; }
        public bool CertificateNameProvided { get; set; }
        public bool Result_ExistingUser { get; set; }
        public bool Result_ExistingIsLocked { get; set; }
        public bool Result_CoachUpgradesNull { get; set; }
        public string Result_LatestUpgradeStatus { get; set; } = string.Empty;
        public bool Result_TermExists { get; set; }
        public bool Result_PhoneExists { get; set; }
        public bool Result_OtpValid { get; set; }
        public string Result_OtpMessage { get; set; } = string.Empty;
        public bool Result_EkycSuccess { get; set; }
        public string Result_EkycError { get; set; } = string.Empty;
        public bool Result_IdCardExists { get; set; }
        public bool Result_AgreementExists { get; set; }
        public bool Result_SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}