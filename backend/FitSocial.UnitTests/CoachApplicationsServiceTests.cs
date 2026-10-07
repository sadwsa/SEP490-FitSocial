using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit tests for UC_29: Staff/Admin View Coach Application Requests.
/// </summary>
public class CoachApplicationsServiceTests
{
    private readonly Mock<ICoachProfileRepository> _coachProfileRepoMock;
    private readonly Mock<ILocationRepository> _locationRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReviewRepository> _reviewRepoMock;
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly CoachService _coachService;

    public CoachApplicationsServiceTests()
    {
        _coachProfileRepoMock = new Mock<ICoachProfileRepository>();
        _locationRepoMock = new Mock<ILocationRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reviewRepoMock = new Mock<IReviewRepository>();
        _orderRepoMock = new Mock<IOrderRepository>();

        _coachService = new CoachService(
            _coachProfileRepoMock.Object,
            _locationRepoMock.Object,
            _unitOfWorkMock.Object,
            _reviewRepoMock.Object,
            _orderRepoMock.Object);
    }

    [Fact]
    public async Task GetCoachApplicationsAsync_WhenApplicationsExist_ReturnsPagedResultAndStatusCounts()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var profiles = new List<CoachProfile>
        {
            new CoachProfile
            {
                CoachId = coachId,
                ApprovalStatus = "PENDING",
                ExperienceYears = 3,
                Bio = "Certified personal trainer.",
                Coach = new User
                {
                    UserId = coachId,
                    FullName = "Tran Quoc Bao",
                    Email = "bao.tran.fitness@gmail.com",
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                },
                CoachCertificates = new List<CoachCertificate>
                {
                    new CoachCertificate { CertificateId = Guid.NewGuid(), CertificateName = "NASM CPT" }
                }
            }
        };

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", 42 },
            { "pending", 28 },
            { "approved", 142 },
            { "rejected", 16 }
        };

        _coachProfileRepoMock.Setup(r => r.GetPagedCoachApplicationsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((profiles, 1));

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        // Act
        var result = await _coachService.GetCoachApplicationsAsync(
            search: "Bao", status: "PENDING", pageNumber: 1, pageSize: 10);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Applications.Items);
        var item = result.Data.Applications.Items[0];
        Assert.Equal("Tran Quoc Bao", item.FullName);
        Assert.Equal("bao.tran.fitness@gmail.com", item.Email);
        Assert.Equal("PENDING", item.ApprovalStatus);
        Assert.Equal(1, item.CertificatesCount);
        Assert.Equal(28, result.Data.StatusCounts.Pending);
        Assert.Equal(42, result.Data.StatusCounts.All);
    }

    [Fact]
    public async Task GetCoachApplicationDetailsAsync_WhenFound_ReturnsFullDetails()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            ExperienceYears = 5,
            Bio = "Expert coach in strength and conditioning.",
            Coach = new User
            {
                UserId = coachId,
                FullName = "Nguyen Thu Thao",
                Email = "thao.yoga@outlook.com",
                Gender = "Female",
                DateOfBirth = new DateOnly(1995, 8, 20),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate
                {
                    CertificateId = Guid.NewGuid(),
                    CertificateName = "Yoga Alliance 200h",
                    IssuedBy = "Yoga Alliance",
                    VerificationStatus = "PENDING"
                }
            },
            CoachEkycVerifications = new List<CoachEkycVerification>
            {
                new CoachEkycVerification
                {
                    EkycId = Guid.NewGuid(),
                    FullNameOnCard = "NGUYEN THU THAO",
                    DateOfBirthOnCard = new DateOnly(1995, 8, 20),
                    VerificationStatus = "COMPLETED",
                    LivenessScore = 0.98m,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _coachService.GetCoachApplicationDetailsAsync(coachId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Nguyen Thu Thao", result.Data.FullName);
        Assert.Equal(5, result.Data.ExperienceYears);
        Assert.Single(result.Data.Certificates);
        Assert.Equal("Yoga Alliance 200h", result.Data.Certificates[0].CertificateName);
        Assert.NotNull(result.Data.Ekyc);
        Assert.Equal("NGUYEN THU THAO", result.Data.Ekyc.FullNameOnCard);
    }

    [Fact]
    public async Task GetCoachApplicationDetailsAsync_WhenNotFound_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachProfile?)null);

        // Act
        var result = await _coachService.GetCoachApplicationDetailsAsync(coachId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Coach application not found.", result.Message);
    }

    [Fact]
    public async Task GetCoachApplicationStatusCountsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", 100 },
            { "pending", 20 },
            { "approved", 70 },
            { "rejected", 10 }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(counts);

        // Act
        var result = await _coachService.GetCoachApplicationStatusCountsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.All);
        Assert.Equal(20, result.Data.Pending);
        Assert.Equal(70, result.Data.Approved);
        Assert.Equal(10, result.Data.Rejected);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenPending_SuccessfullyApprovesAndUnlocksCoach()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var user = new User
        {
            UserId = coachId,
            FullName = "Le Hoang Long",
            Email = "long.le@fit.vn",
            RoleCode = "TRAINEE",
            IsLocked = true
        };
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = user,
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate
                {
                    CertificateId = Guid.NewGuid(),
                    CertificateName = "National Boxing Coach",
                    VerificationStatus = "APPROVED"
                }
            },
            CoachEkycVerifications = new List<CoachEkycVerification>
            {
                new CoachEkycVerification
                {
                    EkycId = Guid.NewGuid(),
                    VerificationStatus = "PENDING"
                }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(
            coachId, approverId, new FitSocial.Application.DTOs.Coach.ApproveCoachApplicationRequestDto { Note = "Valid credentials." });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("APPROVED", profile.ApprovalStatus);
        Assert.Equal(approverId, profile.ApprovedBy);
        Assert.False(user.IsLocked);
        Assert.Equal("COACH", user.RoleCode);
        Assert.Equal("APPROVED", profile.CoachCertificates.First().VerificationStatus);
        Assert.Equal("APPROVED", profile.CoachEkycVerifications.First().VerificationStatus);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenAlreadyApproved_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "APPROVED",
            Coach = new User { UserId = coachId, FullName = "Already Approved" }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, approverId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Only pending applications can be approved", result.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenNotFound_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachProfile?)null);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Coach application not found.", result.Message);
    }

    [Fact]
    public async Task RejectCoachApplicationAsync_WhenPending_SuccessfullyRejectsAndLeavesCoachLocked()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var rejectorId = Guid.NewGuid();
        var user = new User
        {
            UserId = coachId,
            FullName = "Vu Minh Chau",
            Email = "chau.vu@gmail.com",
            IsLocked = true
        };
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = user,
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate
                {
                    CertificateId = Guid.NewGuid(),
                    CertificateName = "Pilates Cert",
                    VerificationStatus = "PENDING"
                }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.RejectCoachApplicationAsync(
            coachId, rejectorId, new FitSocial.Application.DTOs.Coach.RejectCoachApplicationRequestDto { Reason = "Invalid ID card photo." });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        Assert.Equal(rejectorId, profile.ApprovedBy);
        Assert.True(user.IsLocked);
        Assert.Equal("REJECTED", profile.CoachCertificates.First().VerificationStatus);
        Assert.Equal("Invalid ID card photo.", profile.CoachCertificates.First().RejectedReason);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectCoachApplicationAsync_WhenAlreadyProcessed_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var rejectorId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "REJECTED",
            Coach = new User { UserId = coachId, FullName = "Already Rejected" }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _coachService.RejectCoachApplicationAsync(coachId, rejectorId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Only pending applications can be rejected", result.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RejectCoachApplicationAsync_WhenNotFound_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachProfile?)null);

        // Act
        var result = await _coachService.RejectCoachApplicationAsync(coachId, Guid.NewGuid());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Coach application not found.", result.Message);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenNoCertificateApproved_ReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "No Approved Cert" },
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "PENDING" }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, approverId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("At least one certificate must be approved", result.Message);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenAllCertificatesRejected_AutoRejectsAndReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "All Rejected Certs" },
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "REJECTED" },
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "REJECTED" }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, approverId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("all certificates have been rejected", result.Message);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoachApplicationsAsync_WhenHasApprovedCertificate_ReturnsCanApproveTrue()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var profiles = new List<CoachProfile>
        {
            new CoachProfile
            {
                CoachId = coachId,
                ApprovalStatus = "PENDING",
                Coach = new User { UserId = coachId, FullName = "Ready Coach", CreatedAt = DateTime.UtcNow },
                CoachCertificates = new List<CoachCertificate>
                {
                    new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "APPROVED" }
                }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetPagedCoachApplicationsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((profiles, 1));

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _coachService.GetCoachApplicationsAsync();

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var item = Assert.Single(result.Data.Applications.Items);
        Assert.True(item.CanApprove);
        Assert.Equal("PENDING", item.ApprovalStatus);
    }

    [Fact]
    public async Task GetCoachApplicationDetailsAsync_ReturnsAllLivePhotos()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "Photo Coach" },
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "APPROVED" }
            },
            CoachEkycVerifications = new List<CoachEkycVerification>
            {
                new CoachEkycVerification
                {
                    EkycId = Guid.NewGuid(),
                    FaceImageUrl = "https://res.cloudinary.com/demo/image/upload/face1.jpg; https://res.cloudinary.com/demo/image/upload/face2.jpg",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new CoachEkycVerification
                {
                    EkycId = Guid.NewGuid(),
                    FaceImageUrl = "https://res.cloudinary.com/demo/image/upload/face3.jpg",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        // Act
        var result = await _coachService.GetCoachApplicationDetailsAsync(coachId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.CanApprove);
        Assert.Equal(3, result.Data.LivePhotoUrls.Count);
        Assert.Contains("https://res.cloudinary.com/demo/image/upload/face1.jpg", result.Data.LivePhotoUrls);
        Assert.Contains("https://res.cloudinary.com/demo/image/upload/face2.jpg", result.Data.LivePhotoUrls);
        Assert.Contains("https://res.cloudinary.com/demo/image/upload/face3.jpg", result.Data.LivePhotoUrls);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenNoCertificates_AutoRejectsAndReturnsFail()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            CertificateUrl = null,
            Coach = new User { UserId = coachId, FullName = "No Certs Coach" },
            CoachCertificates = new List<CoachCertificate>()
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, approverId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("no certificates were submitted", result.Message);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveCoachApplicationAsync_WhenOneRejectedAndOneApproved_SuccessfullyApproves()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "Mixed Certs Coach" },
            CoachCertificates = new List<CoachCertificate>
            {
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "REJECTED" },
                new CoachCertificate { CertificateId = Guid.NewGuid(), VerificationStatus = "APPROVED" }
            }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.ApproveCoachApplicationAsync(coachId, approverId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("APPROVED", profile.ApprovalStatus);
        Assert.Equal("REJECTED", profile.CoachCertificates.First().VerificationStatus);
        Assert.Equal("APPROVED", profile.CoachCertificates.Last().VerificationStatus);
    }

    [Fact]
    public async Task ReviewCertificateAsync_WhenOneRejectedAndOthersPending_CoachApplicationRemainsPending()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert1Id = Guid.NewGuid();
        var cert2Id = Guid.NewGuid();

        var cert1 = new CoachCertificate { CertificateId = cert1Id, VerificationStatus = "PENDING" };
        var cert2 = new CoachCertificate { CertificateId = cert2Id, VerificationStatus = "PENDING" };

        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "Partial Reject Coach" },
            CoachCertificates = new List<CoachCertificate> { cert1, cert2 }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new FitSocial.Application.DTOs.Coach.ReviewCertificateRequestDto
        {
            Status = "REJECTED",
            Reason = "Blurry certificate photo"
        };

        // Act
        var result = await _coachService.ReviewCertificateAsync(coachId, cert1Id, reviewerId, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("REJECTED", cert1.VerificationStatus);
        Assert.Equal("Blurry certificate photo", cert1.RejectedReason);
        Assert.Equal("PENDING", cert2.VerificationStatus);
        Assert.Equal("PENDING", profile.ApprovalStatus);
        Assert.Equal("PENDING", result.Data.ApprovalStatus);
        Assert.False(result.Data.CanApprove); // Cannot approve yet because none is approved
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewCertificateAsync_WhenAllCertificatesBecomeRejected_AutoRejectsCoachApplication()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert1Id = Guid.NewGuid();
        var cert2Id = Guid.NewGuid();

        var cert1 = new CoachCertificate { CertificateId = cert1Id, VerificationStatus = "REJECTED" };
        var cert2 = new CoachCertificate { CertificateId = cert2Id, VerificationStatus = "PENDING" };

        var user = new User { UserId = coachId, FullName = "All Rejected Coach", IsLocked = false };
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = user,
            CoachCertificates = new List<CoachCertificate> { cert1, cert2 }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new FitSocial.Application.DTOs.Coach.ReviewCertificateRequestDto
        {
            Status = "REJECTED",
            Reason = "Certificate not verified by authority"
        };

        // Act
        var result = await _coachService.ReviewCertificateAsync(coachId, cert2Id, reviewerId, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("REJECTED", cert2.VerificationStatus);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        Assert.Equal("REJECTED", result.Data.ApprovalStatus);
        Assert.False(result.Data.CanApprove);
        Assert.True(user.IsLocked);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewCertificateAsync_WhenOneRejectedAndOneApproved_ApplicationRemainsPendingWithCanApproveTrue()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var cert1Id = Guid.NewGuid();
        var cert2Id = Guid.NewGuid();

        var cert1 = new CoachCertificate { CertificateId = cert1Id, VerificationStatus = "REJECTED" };
        var cert2 = new CoachCertificate { CertificateId = cert2Id, VerificationStatus = "PENDING" };

        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            Coach = new User { UserId = coachId, FullName = "Ready After Cert Review Coach" },
            CoachCertificates = new List<CoachCertificate> { cert1, cert2 }
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new FitSocial.Application.DTOs.Coach.ReviewCertificateRequestDto
        {
            Status = "APPROVED"
        };

        // Act
        var result = await _coachService.ReviewCertificateAsync(coachId, cert2Id, reviewerId, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("APPROVED", cert2.VerificationStatus);
        Assert.Equal("PENDING", profile.ApprovalStatus);
        Assert.Equal("PENDING", result.Data.ApprovalStatus);
        Assert.True(result.Data.CanApprove); // Has at least 1 approved cert, so can be approved now
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoachApplicationDetailsAsync_WhenNoCertificates_AutoRejectsAndSavesToDb()
    {
        // Arrange
        var coachId = Guid.NewGuid();
        var user = new User { UserId = coachId, FullName = "Zero Certs", IsLocked = false };
        var profile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "PENDING",
            CertificateUrl = null,
            Coach = user,
            CoachCertificates = new List<CoachCertificate>()
        };

        _coachProfileRepoMock.Setup(r => r.GetCoachApplicationDetailsAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _coachService.GetCoachApplicationDetailsAsync(coachId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("REJECTED", profile.ApprovalStatus);
        Assert.Equal("REJECTED", result.Data.ApprovalStatus);
        Assert.False(result.Data.CanApprove);
        Assert.True(user.IsLocked);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
