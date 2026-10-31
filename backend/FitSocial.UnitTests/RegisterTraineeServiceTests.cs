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

public class RegisterTraineeServiceTests
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

    public static TheoryData<RegisterTraineeTestCase> TestCases
    {
        get
        {
            var data = new TheoryData<RegisterTraineeTestCase>();
            var assembly = typeof(RegisterTraineeServiceTests).Assembly;
            using var stream = assembly.GetManifestResourceStream("FitSocial.UnitTests.TestData.RegisterTraineeTestCases.csv");
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

                data.Add(new RegisterTraineeTestCase
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
                    EmailExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "EmailExists")),
                    TermExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "TermExists")),
                    PhoneExists = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "PhoneExists")),
                    OtpValid = CsvTestHelper.ParseBool(CsvTestHelper.Get(row, index, "OtpValid")),
                    OtpMessage = string.IsNullOrWhiteSpace(CsvTestHelper.Get(row, index, "OtpMessage")) ? "Invalid or expired OTP code." : CsvTestHelper.Get(row, index, "OtpMessage"),
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
    public async Task RegisterTraineeAccountTest(RegisterTraineeTestCase tc)
    {
        // Reset used mocks
        _users.Reset();
        _unitOfWork.Reset();
        _otp.Reset();
        _terms.Reset();
        _agreements.Reset();
        _passwordHasher.Reset();
        _tokenService.Reset();

        // Defaults
        _passwordHasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed");
        _tokenService.Setup(t => t.CreateSessionAsync(It.IsAny<User>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new AuthResponseDto());
        _users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _agreements.Setup(a => a.AddAsync(It.IsAny<UserAgreement>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _users.Setup(u => u.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(tc.EmailExists);

        var phone = string.IsNullOrWhiteSpace(tc.PhoneRaw) ? null : tc.PhoneRaw.Trim();
        if (phone != null)
        {
            _users.Setup(u => u.ExistsByPhoneAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(tc.PhoneExists);
        }

        Guid? termId = ParseTermId(tc.TermRaw);
        if (termId.HasValue && termId.Value != Guid.Empty)
        {
            if (tc.TermExists)
            {
                _terms.Setup(t => t.GetByIdAsync(termId.Value, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new TermsAndPolicy { TermId = termId.Value });
            }
            else
            {
                _terms.Setup(t => t.GetByIdAsync(termId.Value, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((TermsAndPolicy?)null);
            }
        }

        _otp.Setup(o => o.ValidateOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((tc.OtpValid, tc.OtpValid ? "OTP verified." : tc.OtpMessage));

        if (tc.SaveChangesThrows)
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateException("db fail"));
        }
        else
        {
            _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(tc.DobRaw) && !string.Equals(tc.DobRaw, "EMPTY", StringComparison.OrdinalIgnoreCase))
        {
            dob = DateOnly.Parse(tc.DobRaw);
        }

        var request = new RegisterTraineeRequestDto
        {
            FullName = tc.FullName,
            Email = tc.Email,
            Password = tc.Password,
            ConfirmPassword = tc.ConfirmPassword,
            OtpCode = tc.OtpCode,
            PhoneNumber = tc.PhoneRaw,
            Gender = string.IsNullOrWhiteSpace(tc.GenderRaw) ? null : tc.GenderRaw,
            DateOfBirth = dob,
            TermId = termId
        };

        var sut = CreateSut();
        var result = await sut.RegisterTraineeAsync(request);

        Assert.True(result.Success == tc.ExpectedSuccess && result.Message == tc.ExpectedMessage,
            $"{tc.UtcId} ({tc.Type}): expected Success={tc.ExpectedSuccess}, Message=\"{tc.ExpectedMessage}\" but got Success={result.Success}, Message=\"{result.Message}\"");
    }

    private static Guid? ParseTermId(string value)
    {
        var v = value?.Trim();
        if (string.IsNullOrWhiteSpace(v) || string.Equals(v, "EMPTY", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(v, "EMPTYGUID", StringComparison.OrdinalIgnoreCase))
            return Guid.Empty;
        return Guid.Parse(v);
    }
    public class RegisterTraineeTestCase
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
        public bool EmailExists { get; set; }
        public bool TermExists { get; set; }
        public bool PhoneExists { get; set; }
        public bool OtpValid { get; set; }
        public string OtpMessage { get; set; } = string.Empty;
        public bool SaveChangesThrows { get; set; }
        public bool ExpectedSuccess { get; set; }
        public string ExpectedMessage { get; set; } = string.Empty;
        public override string ToString() => UtcId;
    }
}
