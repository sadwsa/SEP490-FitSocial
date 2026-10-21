using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachCertificatePublicProfileTests
{
    private readonly DateOnly _mockToday = new(2026, 10, 21);

    #region 1. CoachCertificatePolicy Domain Rules

    [Fact]
    public void Policy_ApprovedAndFutureExpiry_IsValidAndNotExpired()
    {
        // Case 1: Coach có chứng chỉ đã duyệt và còn hạn
        var cert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "NASM Certified Personal Trainer",
            CertificateUrl = "https://example.com/valid.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday.AddMonths(6)
        };

        Assert.True(CoachCertificatePolicy.IsCertificateValid(cert, _mockToday));
        Assert.False(CoachCertificatePolicy.IsCertificateExpired(cert, _mockToday));
    }

    [Fact]
    public void Policy_ApprovedAndPastExpiry_IsExpiredAndNotValid()
    {
        // Case 2: Coach có chứng chỉ đã duyệt nhưng hết hạn
        var cert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "CPR/AED",
            CertificateUrl = "https://example.com/expired.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday.AddDays(-1)
        };

        Assert.True(CoachCertificatePolicy.IsCertificateExpired(cert, _mockToday));
        Assert.False(CoachCertificatePolicy.IsCertificateValid(cert, _mockToday));
    }

    [Fact]
    public void Policy_BothValidAndExpired_OnlyValidReturnedInListAndExpiredDetected()
    {
        // Case 3: Coach có cả chứng chỉ hợp lệ và hết hạn
        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Valid NASM",
            CertificateUrl = "https://example.com/valid.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday.AddMonths(12)
        };
        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired CPR",
            CertificateUrl = "https://example.com/expired.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday.AddDays(-5)
        };

        var certs = new List<CoachCertificate> { validCert, expiredCert };

        Assert.True(CoachCertificatePolicy.HasExpiredCertificates(certs, _mockToday));
        Assert.True(CoachCertificatePolicy.HasValidCertificates(certs, _mockToday));

        var validList = CoachCertificatePolicy.GetValidCertificates(certs, _mockToday);
        Assert.Single(validList);
        Assert.Equal(validCert.CertificateId, validList[0].CertificateId);
    }

    [Fact]
    public void Policy_NoCertificates_HasValidFalseAndHasExpiredFalse()
    {
        // Case 4: Coach không có chứng chỉ nào
        var certs = new List<CoachCertificate>();

        Assert.False(CoachCertificatePolicy.HasExpiredCertificates(certs, _mockToday));
        Assert.False(CoachCertificatePolicy.HasValidCertificates(certs, _mockToday));
        Assert.Empty(CoachCertificatePolicy.GetValidCertificates(certs, _mockToday));
    }

    [Fact]
    public void Policy_PendingOrRejected_NeverConsideredValid()
    {
        // Case 5: Coach chỉ có chứng chỉ đang chờ duyệt hoặc bị từ chối
        var pendingCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Pending Cert",
            CertificateUrl = "https://example.com/pending.pdf",
            VerificationStatus = "PENDING",
            ExpiryDate = _mockToday.AddYears(1)
        };
        var rejectedCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Rejected Cert",
            CertificateUrl = "https://example.com/rejected.pdf",
            VerificationStatus = "REJECTED",
            ExpiryDate = _mockToday.AddYears(1)
        };

        var certs = new List<CoachCertificate> { pendingCert, rejectedCert };

        Assert.False(CoachCertificatePolicy.IsCertificateValid(pendingCert, _mockToday));
        Assert.False(CoachCertificatePolicy.IsCertificateValid(rejectedCert, _mockToday));
        Assert.False(CoachCertificatePolicy.HasValidCertificates(certs, _mockToday));
        Assert.Empty(CoachCertificatePolicy.GetValidCertificates(certs, _mockToday));
    }

    [Fact]
    public void Policy_MissingExpiryDate_NeverAssumedValid()
    {
        // Case 6: Chứng chỉ không có ngày hết hạn
        var noExpiryCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Undated Certificate",
            CertificateUrl = "https://example.com/undated.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = null
        };

        Assert.False(CoachCertificatePolicy.IsCertificateValid(noExpiryCert, _mockToday));
        Assert.False(CoachCertificatePolicy.IsCertificateExpired(noExpiryCert, _mockToday));
    }

    [Fact]
    public void Policy_BoundaryDateCheck_TodayIsStillValid_YesterdayIsExpired()
    {
        // Case 7: Kiểm tra ranh giới ngày hết hạn
        var expiresToday = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expires Today",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday
        };

        var expiredYesterday = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired Yesterday",
            VerificationStatus = "APPROVED",
            ExpiryDate = _mockToday.AddDays(-1)
        };

        Assert.True(CoachCertificatePolicy.IsCertificateValid(expiresToday, _mockToday));
        Assert.False(CoachCertificatePolicy.IsCertificateExpired(expiresToday, _mockToday));

        Assert.False(CoachCertificatePolicy.IsCertificateValid(expiredYesterday, _mockToday));
        Assert.True(CoachCertificatePolicy.IsCertificateExpired(expiredYesterday, _mockToday));
    }

    #endregion

    #region 2. CoachService.GetCoachDetailsAsync (API Security & Sanitization)

    [Fact]
    public async Task CoachService_GetCoachDetailsAsync_NeverExposesExpiredOrUnapprovedCertificateUrls()
    {
        // Case 8: Người dùng truy cập API trực tiếp: không thể lấy URL của chứng chỉ hết hạn qua API công khai
        var coachId = Guid.NewGuid();
        var mockCoachRepo = new Mock<ICoachProfileRepository>();
        var mockLocRepo = new Mock<ILocationRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var mockReviewRepo = new Mock<IReviewRepository>();
        var mockOrderRepo = new Mock<IOrderRepository>();

        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Valid Master Trainer",
            CertificateUrl = "https://example.com/public-valid.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3))
        };

        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired Private Certificate",
            CertificateUrl = "https://example.com/private-expired-secret.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10))
        };

        var pendingCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Pending Secret Certificate",
            CertificateUrl = "https://example.com/private-pending-secret.pdf",
            VerificationStatus = "PENDING",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6))
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            Bio = "Top Fitness Coach",
            Coach = new User { UserId = coachId, FullName = "Alex Coach" },
            CoachCertificates = new List<CoachCertificate> { validCert, expiredCert, pendingCert },
            Locations = new List<Location>(),
            Reviews = new List<Review>(),
            TrainingPackages = new List<TrainingPackage>()
        };

        mockCoachRepo.Setup(r => r.FindWithDetailsByCoachIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        var service = new CoachService(
            mockCoachRepo.Object,
            mockLocRepo.Object,
            mockUow.Object,
            mockReviewRepo.Object,
            mockOrderRepo.Object);

        // Act
        var result = await service.GetCoachDetailsAsync(coachId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var dto = result.Data!;

        // Flag checks
        Assert.True(dto.HasExpiredCertificates);
        Assert.True(dto.HasValidCertificates);

        // URL Security checks
        Assert.Single(dto.Certificates);
        Assert.Equal(validCert.CertificateId, dto.Certificates[0].CertificateId);
        Assert.Equal("https://example.com/public-valid.pdf", dto.Certificates[0].CertificateUrl);

        // Expired and Pending URLs must NOT be present anywhere in the returned Certificates list
        Assert.DoesNotContain(dto.Certificates, c => c.CertificateUrl == "https://example.com/private-expired-secret.pdf");
        Assert.DoesNotContain(dto.Certificates, c => c.CertificateUrl == "https://example.com/private-pending-secret.pdf");
    }

    #endregion

    #region 3. UserService.GetUserProfileAsync (Public User Profile)

    [Fact]
    public async Task UserService_GetUserProfileAsync_ReturnsOnlyValidCertificatesAndFlagsForCoach()
    {
        var coachId = Guid.NewGuid();
        var mockUserRepo = new Mock<IUserRepository>();

        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired Crossfit L1",
            CertificateUrl = "https://example.com/secret-expired.pdf",
            VerificationStatus = "APPROVED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20))
        };

        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Elena Coach",
            RoleCode = RoleConstants.Coach,
            CoachProfileCoach = new CoachProfile
            {
                CoachId = coachId,
                Bio = "Crossfit Head Coach",
                CoachCertificates = new List<CoachCertificate> { expiredCert },
                Locations = new List<Location>(),
                Reviews = new List<Review>()
            }
        };

        mockUserRepo.Setup(r => r.FindUserProfileByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        var service = new UserService(mockUserRepo.Object);

        // Act
        var result = await service.GetUserProfileAsync(coachId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var coachDto = Assert.IsType<CoachProfileResponseDto>(result.Data);

        Assert.True(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates); // Expired cert is excluded
    }

    #endregion

    #region 4. TrainingPackageService (Purchase flow warnings)

    [Fact]
    public async Task TrainingPackageService_GetPackageByIdAsync_MapsCoachCertificateFlagsProperly()
    {
        // Case 9: Kiểm tra cảnh báo trong luồng mua Training Plan
        var packageId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var mockRepo = new Mock<ITrainingPackageRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var mockReviewRepo = new Mock<IReviewRepository>();
        var mockOrderRepo = new Mock<IOrderRepository>();

        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired Level 2",
            VerificationStatus = "APPROVED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))
        };

        var package = new TrainingPackage
        {
            PackageId = packageId,
            CoachId = coachId,
            Title = "30-Day Transformation Plan",
            Price = 1000000,
            DurationDays = 30,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User { UserId = coachId, FullName = "Marcus Coach" },
                CoachCertificates = new List<CoachCertificate> { expiredCert }
            }
        };

        mockRepo.Setup(r => r.GetByIdAsync(packageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        var service = new TrainingPackageService(
            mockRepo.Object,
            mockUow.Object,
            mockReviewRepo.Object,
            mockOrderRepo.Object);

        // Act
        var result = await service.GetPackageByIdAsync(packageId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.CoachHasExpiredCertificates);
        Assert.False(result.CoachHasValidCertificates);
    }

    #endregion
}
