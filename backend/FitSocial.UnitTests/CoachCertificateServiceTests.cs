using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachCertificateServiceTests
{
    private readonly Mock<ICoachCertificateRepository> _mockCertRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuditLogRepository> _mockAuditLogs;
    private readonly Mock<INotificationRepository> _mockNotifications;
    private readonly CoachCertificateService _service;

    public CoachCertificateServiceTests()
    {
        _mockCertRepo = new Mock<ICoachCertificateRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockAuditLogs = new Mock<IAuditLogRepository>();
        _mockNotifications = new Mock<INotificationRepository>();

        _service = new CoachCertificateService(
            _mockCertRepo.Object,
            _mockCoachRepo.Object,
            _mockUow.Object,
            _mockAuditLogs.Object,
            _mockNotifications.Object);
    }

    [Fact]
    public async Task GetCertificatesAsync_ReturnsPagedListAndStatusCounts()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coachId,
            CertificateName = "ACE Certified Trainer",
            CertificateUrl = "https://example.com/cert.pdf",
            VerificationStatus = "PENDING",
            CreatedAt = DateTime.UtcNow,
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User { UserId = coachId, FullName = "John Coach", Email = "john@example.com" }
            }
        };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachCertificate> { cert }, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>
            {
                ["ALL"] = 1,
                ["PENDING"] = 1,
                ["APPROVED"] = 0,
                ["REJECTED"] = 0
            });

        // Act
        var result = await _service.GetCertificatesAsync(new GetCoachCertificatesAdminQueryDto());

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("ACE Certified Trainer", result.Data.Certificates.Items[0].CertificateName);
        Assert.Equal("John Coach", result.Data.Certificates.Items[0].CoachName);
        Assert.Equal(1, result.Data.StatusCounts.Pending);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_WhenExists_ReturnsCertificateDetails()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coachId,
            CertificateName = "NASM CPT",
            CertificateUrl = "https://example.com/nasm.jpg",
            VerificationStatus = "APPROVED",
            IssuedBy = "National Academy of Sports Medicine",
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Bio = "Certified fitness coach with 5 years experience.",
                ExperienceYears = 5,
                Coach = new User { UserId = coachId, FullName = "Alice Coach", Email = "alice@example.com" }
            }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("NASM CPT", result.Data.CertificateName);
        Assert.Equal(5, result.Data.CoachExperienceYears);
        Assert.Equal("Alice Coach", result.Data.CoachName);
    }

    [Fact]
    public async Task GetCertificateByIdAsync_WhenNotFound_ReturnsFail()
    {
        // Arrange
        var certId = Guid.NewGuid();
        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachCertificate?)null);

        // Act
        var result = await _service.GetCertificateByIdAsync(certId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveCertificateAsync_WhenPending_UpdatesStatusApproved_AddsNotificationAndAuditLog()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coachId,
            CertificateName = "CrossFit Level 2",
            CertificateUrl = "https://example.com/cf.pdf",
            VerificationStatus = "PENDING",
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User { UserId = coachId, FullName = "Bob Coach", Email = "bob@example.com" }
            }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.ApproveCertificateAsync(certId, reviewerId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("APPROVED", cert.VerificationStatus);
        Assert.Equal(reviewerId, cert.VerifiedBy);
        Assert.NotNull(cert.VerifiedAt);

        _mockCertRepo.Verify(r => r.Update(cert), Times.Once);
        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.UserId == coachId &&
            x.Type == "COACH_CERTIFICATE_APPROVED" &&
            x.ReferenceId == certId), It.IsAny<CancellationToken>()), Times.Once);

        _mockAuditLogs.Verify(a => a.AddAsync(It.Is<AuditLog>(x =>
            x.ActorAccountId == reviewerId &&
            x.Action == "APPROVE_COACH_CERTIFICATE" &&
            x.EntityId == certId.ToString()), It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveCertificateAsync_WhenAlreadyApproved_ReturnsFail()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            VerificationStatus = "APPROVED"
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        // Act
        var result = await _service.ApproveCertificateAsync(certId, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already been approved", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectCertificateAsync_WhenPending_UpdatesStatusRejected_AddsNotificationAndAuditLog()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coachId,
            CertificateName = "Blurry Diploma",
            VerificationStatus = "PENDING",
            Coach = new CoachProfile
            {
                CoachId = coachId,
                Coach = new User { UserId = coachId, FullName = "Claire Coach", Email = "claire@example.com" }
            }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockCoachRepo.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CoachProfile
            {
                CoachId = coachId,
                ApprovalStatus = "PENDING",
                CoachCertificates = new List<CoachCertificate> { cert, new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "PENDING" } }
            });
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new RejectCoachCertificateDto
        {
            Reason = "Certificate expired in 2021 and illegible signature."
        };

        // Act
        var result = await _service.RejectCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("REJECTED", cert.VerificationStatus);
        Assert.Equal("Certificate expired in 2021 and illegible signature.", cert.RejectedReason);
        Assert.Equal(reviewerId, cert.VerifiedBy);
        Assert.NotNull(cert.VerifiedAt);

        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.UserId == coachId &&
            x.Type == "COACH_CERTIFICATE_REJECTED" &&
            x.Description != null && x.Description.Contains("illegible signature")), It.IsAny<CancellationToken>()), Times.Once);

        _mockAuditLogs.Verify(a => a.AddAsync(It.Is<AuditLog>(x =>
            x.ActorAccountId == reviewerId &&
            x.Action == "REJECT_COACH_CERTIFICATE"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectCertificateAsync_WhenReasonIsEmpty_ReturnsFail()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            VerificationStatus = "PENDING"
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new RejectCoachCertificateDto { Reason = "   " };

        // Act
        var result = await _service.RejectCertificateAsync(certId, Guid.NewGuid(), request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("reason is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectCertificateAsync_WhenAlreadyRejected_ReturnsFail()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            VerificationStatus = "REJECTED"
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new RejectCoachCertificateDto { Reason = "Invalid." };

        // Act
        var result = await _service.RejectCertificateAsync(certId, Guid.NewGuid(), request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("already been rejected", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectCertificateAsync_WhenAllCertificatesRejected_AutoRejectsCoachApplication()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coachId,
            VerificationStatus = "PENDING"
        };

        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "Dave Coach" },
            CoachCertificates = new List<CoachCertificate> { cert }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockCoachRepo.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new RejectCoachCertificateDto { Reason = "Forged accreditation document." };

        // Act
        var result = await _service.RejectCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("REJECTED", cert.VerificationStatus);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        Assert.True(profile.Coach.IsLocked);

        // Both cert rejection notification and application auto-rejection notification
        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.Type == "COACH_APPLICATION_REJECTED"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
