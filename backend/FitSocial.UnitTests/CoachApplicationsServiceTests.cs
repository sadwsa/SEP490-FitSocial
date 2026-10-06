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
}
