using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewCoachCertificateDetailsServiceTests
{
    private readonly Mock<ICoachCertificateRepository> _mockCertRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CoachCertificateService _service;

    public ViewCoachCertificateDetailsServiceTests()
    {
        _mockCertRepo = new Mock<ICoachCertificateRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new CoachCertificateService(
            _mockCertRepo.Object,
            _mockCoachRepo.Object,
            _mockUow.Object);
    }

    private static CoachCertificate CreateCertificate(
        Guid? certId = null,
        Guid? coachId = null,
        string name = "NASM Certified Personal Trainer",
        string status = "APPROVED",
        string? rejectedReason = null,
        Guid? verifiedBy = null)
    {
        var cId = certId ?? Guid.NewGuid();
        var coId = coachId ?? Guid.NewGuid();
        var vId = verifiedBy ?? Guid.NewGuid();

        return new CoachCertificate
        {
            CertificateId = cId,
            CoachId = coId,
            CertificateName = name,
            CertificateUrl = "https://example.com/nasm.pdf",
            IssuedBy = "National Academy of Sports Medicine",
            IssuedDate = new DateOnly(2023, 5, 10),
            ExpiryDate = new DateOnly(2025, 5, 10),
            VerificationStatus = status,
            VerifiedBy = status == "APPROVED" || status == "REJECTED" ? vId : null,
            VerifiedAt = status == "APPROVED" || status == "REJECTED" ? DateTime.UtcNow.AddDays(-1) : null,
            RejectedReason = rejectedReason,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Coach = new CoachProfile
            {
                CoachId = coId,
                Bio = "Certified fitness coach with 5 years experience.",
                ExperienceYears = 5,
                ApprovalStatus = "APPROVED",
                Coach = new User
                {
                    UserId = coId,
                    FullName = "Tran Coach",
                    Email = "coach@example.com",
                    PhoneNumber = "0901234567",
                    AvatarUrl = "https://example.com/coach.jpg"
                }
            },
            VerifiedByNavigation = status == "APPROVED" || status == "REJECTED"
                ? new User
                {
                    UserId = vId,
                    FullName = "Admin Staff",
                    Email = "admin@fitsocial.com"
                }
                : null
        };
    }

    [Fact]
    public async Task GetCertificateByIdAsync_ApprovedCertificateWithFullDetails_ReturnsSuccessWithMappedDto()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "APPROVED");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Certificate details retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(certId, result.Data.CertificateId);
        Assert.Equal("NASM Certified Personal Trainer", result.Data.CertificateName);
        Assert.Equal("APPROVED", result.Data.VerificationStatus);
        Assert.Equal("Tran Coach", result.Data.CoachName);
        Assert.Equal("coach@example.com", result.Data.CoachEmail);
        Assert.Equal("0901234567", result.Data.CoachPhoneNumber);
        Assert.Equal(5, result.Data.CoachExperienceYears);
        Assert.Equal("Admin Staff", result.Data.VerifiedByName);
        Assert.Equal("admin@fitsocial.com", result.Data.VerifiedByEmail);
        _mockCertRepo.Verify(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_PendingCertificate_ReturnsSuccessWithPendingStatus()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("PENDING", result.Data.VerificationStatus);
        Assert.Null(result.Data.VerifiedBy);
        Assert.Null(result.Data.VerifiedByName);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_RejectedCertificate_ReturnsSuccessWithRejectedReason()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = CreateCertificate(
            certId: certId,
            status: "REJECTED",
            rejectedReason: "The certificate image is blurry and cannot be verified.");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("REJECTED", result.Data.VerificationStatus);
        Assert.Equal("The certificate image is blurry and cannot be verified.", result.Data.RejectedReason);
        Assert.Equal("Admin Staff", result.Data.VerifiedByName);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_WithNullCoachNavigation_ReturnsSuccessWithDefaultUnknownCoach()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = Guid.NewGuid(),
            CertificateName = "CrossFit Level 1",
            VerificationStatus = "PENDING",
            Coach = null!,
            VerifiedByNavigation = null
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Unknown Coach", result.Data.CoachName);
        Assert.Equal(string.Empty, result.Data.CoachEmail);
        Assert.Null(result.Data.CoachBio);
        Assert.Null(result.Data.CoachExperienceYears);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_EmptyGuid_ReturnsCertificateNotFound()
    {
        // Arrange
        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachCertificate?)null);

        // Act
        var result = await _service.GetCertificateByIdAsync(Guid.Empty);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate not found.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_CertificateNotFound_ReturnsFailResponse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachCertificate?)null);

        // Act
        var result = await _service.GetCertificateByIdAsync(nonExistentId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate not found.", result.Message);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_RepositoryThrowsException_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Database connection failure", result.Message);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_CancelledToken_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.GetCertificateByIdAsync(certId, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error retrieving certificate", result.Message);
    }
}

