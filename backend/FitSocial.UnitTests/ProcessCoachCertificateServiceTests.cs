using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.DTOs.Notifications;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ProcessCoachCertificateServiceTests
{
    private readonly Mock<ICoachCertificateRepository> _mockCertRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IAuditLogRepository> _mockAuditLogs;
    private readonly Mock<INotificationRepository> _mockNotifications;
    private readonly Mock<IUserRepository> _mockUsers;
    private readonly Mock<IChatRealtimeNotifier> _mockRealtimeNotifier;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly CoachCertificateService _service;

    public ProcessCoachCertificateServiceTests()
    {
        _mockCertRepo = new Mock<ICoachCertificateRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockAuditLogs = new Mock<IAuditLogRepository>();
        _mockNotifications = new Mock<INotificationRepository>();
        _mockUsers = new Mock<IUserRepository>();
        _mockRealtimeNotifier = new Mock<IChatRealtimeNotifier>();
        _mockAuditLogService = new Mock<IAuditLogService>();

        _service = new CoachCertificateService(
            _mockCertRepo.Object,
            _mockCoachRepo.Object,
            _mockUow.Object,
            _mockAuditLogs.Object,
            _mockNotifications.Object,
            _mockUsers.Object,
            _mockRealtimeNotifier.Object,
            _mockAuditLogService.Object);
    }

    private static CoachCertificate CreateCertificate(
        Guid? certId = null,
        Guid? coachId = null,
        string name = "NASM Certified Personal Trainer",
        string status = "PENDING",
        string? rejectedReason = null,
        Guid? verifiedBy = null)
    {
        var cId = certId ?? Guid.NewGuid();
        var coId = coachId ?? Guid.NewGuid();

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
            VerifiedBy = verifiedBy,
            VerifiedAt = verifiedBy.HasValue ? DateTime.UtcNow.AddDays(-1) : null,
            RejectedReason = rejectedReason,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            Coach = new CoachProfile
            {
                CoachId = coId,
                Bio = "Certified fitness coach with 5 years experience.",
                ExperienceYears = 5,
                ApprovalStatus = "PENDING",
                Coach = new User
                {
                    UserId = coId,
                    FullName = "Tran Coach",
                    Email = "coach@example.com",
                    PhoneNumber = "0901234567"
                }
            }
        };
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionApprove_PendingCertificate_SuccessfullyApproves()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ProcessCoachCertificateDto
        {
            Action = "APPROVE"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Coach certificate approved successfully.", result.Message);
        Assert.Equal("APPROVED", cert.VerificationStatus);
        Assert.Equal(reviewerId, cert.VerifiedBy);
        Assert.NotNull(cert.VerifiedAt);
        Assert.Null(cert.RejectedReason);

        _mockCertRepo.Verify(r => r.Update(cert), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.UserId == cert.CoachId &&
            x.Type == "COACH_CERTIFICATE_APPROVED" &&
            x.ReferenceId == certId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_ValidReason_SuccessfullyRejects()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, coachId: coachId, status: "PENDING");
        var otherCert = CreateCertificate(coachId: coachId, status: "PENDING");

        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            CoachCertificates = new List<CoachCertificate> { cert, otherCert },
            Coach = new User { UserId = coachId, FullName = "Tran Coach", IsLocked = false }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockCoachRepo.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = "Certificate expired and illegible signature."
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Coach certificate rejected successfully.", result.Message);
        Assert.Equal("REJECTED", cert.VerificationStatus);
        Assert.Equal("Certificate expired and illegible signature.", cert.RejectedReason);
        Assert.Equal(reviewerId, cert.VerifiedBy);
        Assert.Equal("PENDING", profile.ApprovalStatus);
        Assert.False(profile.Coach.IsLocked);

        _mockCertRepo.Verify(r => r.Update(cert), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.UserId == coachId &&
            x.Type == "COACH_CERTIFICATE_REJECTED" &&
            x.ReferenceId == certId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_AllCertsRejected_AutoRejectsCoachProfile()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, coachId: coachId, status: "PENDING");

        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            CoachCertificates = new List<CoachCertificate> { cert },
            Coach = new User { UserId = coachId, FullName = "Tran Coach", IsLocked = false }
        };

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockCoachRepo.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = "Invalid accreditation credentials."
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("REJECTED", cert.VerificationStatus);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        Assert.True(profile.Coach.IsLocked);

        _mockNotifications.Verify(n => n.AddAsync(It.Is<Notification>(x =>
            x.UserId == coachId &&
            x.Type == "COACH_APPLICATION_REJECTED"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_ReasonAtMaxBoundary1000Chars_SuccessfullyRejects()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");
        var exactly1000Chars = new string('A', 1000);

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = exactly1000Chars
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("REJECTED", cert.VerificationStatus);
        Assert.Equal(exactly1000Chars, cert.RejectedReason);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_ReasonExceeds1000Chars_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");
        var over1000Chars = new string('B', 1001);

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = over1000Chars
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Rejection reason cannot exceed 1000 characters.", result.Message);
        _mockCertRepo.Verify(r => r.Update(It.IsAny<CoachCertificate>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_WhitespaceReason_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = "   "
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Rejection reason is required.", result.Message);
        _mockCertRepo.Verify(r => r.Update(It.IsAny<CoachCertificate>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCertificateAsync_NullOrWhitespaceAction_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var request = new ProcessCoachCertificateDto
        {
            Action = "   "
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Action is required ('APPROVE' or 'REJECT').", result.Message);
        _mockCertRepo.Verify(r => r.GetCertificateWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessCertificateAsync_InvalidAction_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var request = new ProcessCoachCertificateDto
        {
            Action = "UNKNOWN_ACTION"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Invalid action 'UNKNOWN_ACTION'", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_CertificateNotFound_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachCertificate?)null);

        var request = new ProcessCoachCertificateDto
        {
            Action = "APPROVE"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate not found.", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionApprove_AlreadyApproved_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "APPROVED");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new ProcessCoachCertificateDto
        {
            Action = "APPROVE"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate has already been approved.", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionApprove_AlreadyRejected_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "REJECTED");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new ProcessCoachCertificateDto
        {
            Action = "APPROVE"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate has already been reviewed and rejected.", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_ActionReject_InvalidCurrentStatus_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "REVOKED");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = "Revoked certificate cannot be rejected."
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Certificate cannot be rejected from status 'REVOKED'.", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_DatabaseExceptionOnSave_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert = CreateCertificate(certId: certId, status: "PENDING");

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        var request = new ProcessCoachCertificateDto
        {
            Action = "APPROVE"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Database connection failure", result.Message);
    }

    [Fact]
    public async Task ProcessCertificateAsync_CancelledToken_ReturnsFailResponse()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCertRepo.Setup(r => r.GetCertificateWithDetailsAsync(certId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var request = new ProcessCoachCertificateDto
        {
            Action = "REJECT",
            Reason = "Cancelled operation"
        };

        // Act
        var result = await _service.ProcessCertificateAsync(certId, reviewerId, request, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error rejecting certificate", result.Message);
    }
}

