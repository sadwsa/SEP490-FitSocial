using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachCertificate;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewCoachCertificateListServiceTests
{
    private readonly Mock<ICoachCertificateRepository> _mockCertRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly CoachCertificateService _service;

    public ViewCoachCertificateListServiceTests()
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
        string name = "ACE Certified Trainer",
        string status = "PENDING",
        string coachName = "John Coach",
        string coachEmail = "john@example.com")
    {
        var cId = certId ?? Guid.NewGuid();
        var coId = coachId ?? Guid.NewGuid();

        return new CoachCertificate
        {
            CertificateId = cId,
            CoachId = coId,
            CertificateName = name,
            CertificateUrl = "https://example.com/cert.pdf",
            IssuedBy = "American Council on Exercise",
            IssuedDate = new DateOnly(2023, 1, 1),
            ExpiryDate = new DateOnly(2025, 1, 1),
            VerificationStatus = status,
            CreatedAt = DateTime.UtcNow,
            Coach = new CoachProfile
            {
                CoachId = coId,
                Coach = new User
                {
                    UserId = coId,
                    FullName = coachName,
                    Email = coachEmail,
                    PhoneNumber = "0987654321",
                    AvatarUrl = "https://example.com/avatar.jpg"
                }
            }
        };
    }

    [Fact]
    public async Task GetCertificatesAsync_DefaultQuery_ReturnsPagedCertificatesWithStatusCounts()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();
        var list = new List<CoachCertificate>
        {
            CreateCertificate(status: "PENDING"),
            CreateCertificate(status: "APPROVED")
        };
        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 2));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>
            {
                ["ALL"] = 2,
                ["PENDING"] = 1,
                ["APPROVED"] = 1,
                ["REJECTED"] = 0
            });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Coach certificates retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Certificates.TotalCount);
        Assert.Equal(2, result.Data.Certificates.Items.Count);
        Assert.Equal(2, result.Data.StatusCounts.All);
        Assert.Equal(1, result.Data.StatusCounts.Pending);
        Assert.Equal(1, result.Data.StatusCounts.Approved);
        Assert.Equal(0, result.Data.StatusCounts.Rejected);
        _mockCertRepo.Verify(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCertificatesAsync_FilterByPendingStatus_ReturnsFilteredPendingCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "PENDING", PageNumber = 1, PageSize = 10 };
        var list = new List<CoachCertificate> { CreateCertificate(status: "PENDING") };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "PENDING", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["PENDING"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("PENDING", result.Data.Certificates.Items[0].VerificationStatus);
    }

    [Fact]
    public async Task GetCertificatesAsync_FilterByApprovedStatus_ReturnsFilteredApprovedCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "APPROVED", PageNumber = 1, PageSize = 10 };
        var list = new List<CoachCertificate> { CreateCertificate(status: "APPROVED") };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "APPROVED", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["APPROVED"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("APPROVED", result.Data.Certificates.Items[0].VerificationStatus);
    }

    [Fact]
    public async Task GetCertificatesAsync_FilterByRejectedStatus_ReturnsFilteredRejectedCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "REJECTED", PageNumber = 1, PageSize = 10 };
        var list = new List<CoachCertificate> { CreateCertificate(status: "REJECTED") };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "REJECTED", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["REJECTED"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("REJECTED", result.Data.Certificates.Items[0].VerificationStatus);
    }

    [Fact]
    public async Task GetCertificatesAsync_FilterBySearchKeyword_ReturnsMatchingCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Search = "NASM", Status = "ALL", PageNumber = 1, PageSize = 10 };
        var list = new List<CoachCertificate> { CreateCertificate(name: "NASM CPT") };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync("NASM", "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("NASM CPT", result.Data.Certificates.Items[0].CertificateName);
    }

    [Fact]
    public async Task GetCertificatesAsync_CustomSortBy_PassesSortParameterAndReturnsResults()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { SortBy = "date_asc", Status = "ALL", PageNumber = 1, PageSize = 10 };
        var list = new List<CoachCertificate> { CreateCertificate() };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_asc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
    }

    [Fact]
    public async Task GetCertificatesAsync_WithNullCoachNavigation_MapsWithDefaultUnknownCoach()
    {
        // Arrange
        var certId = Guid.NewGuid();
        var cert = new CoachCertificate
        {
            CertificateId = certId,
            CoachId = Guid.NewGuid(),
            CertificateName = "CrossFit Level 1",
            VerificationStatus = "PENDING",
            Coach = null!
        };
        var query = new GetCoachCertificatesAdminQueryDto();

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachCertificate> { cert }, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("Unknown Coach", result.Data.Certificates.Items[0].CoachName);
        Assert.Equal(string.Empty, result.Data.Certificates.Items[0].CoachEmail);
    }

    [Fact]
    public async Task GetCertificatesAsync_EmptyResult_ReturnsEmptyPagedListWithZeroCounts()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachCertificate>(), 0));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Data.Certificates.Items);
        Assert.Equal(0, result.Data.Certificates.TotalCount);
        Assert.Equal(0, result.Data.Certificates.TotalPages);
        Assert.Equal(0, result.Data.StatusCounts.All);
        Assert.Equal(0, result.Data.StatusCounts.Pending);
    }

    [Fact]
    public async Task GetCertificatesAsync_CustomPagination_ReturnsCorrectPageAndPageSize()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { PageNumber = 2, PageSize = 5 };
        var list = new List<CoachCertificate>
        {
            CreateCertificate(),
            CreateCertificate(),
            CreateCertificate(),
            CreateCertificate(),
            CreateCertificate()
        };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 15));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["ALL"] = 15 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data.Certificates.PageNumber);
        Assert.Equal(5, result.Data.Certificates.PageSize);
        Assert.Equal(15, result.Data.Certificates.TotalCount);
        Assert.Equal(3, result.Data.Certificates.TotalPages);
        Assert.Equal(5, result.Data.Certificates.Items.Count);
    }

    [Fact]
    public async Task GetCertificatesAsync_NullQuery_ReturnsFailResponse()
    {
        // Arrange & Act
        var result = await _service.GetCertificatesAsync(null!);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error retrieving coach certificates", result.Message);
    }

    [Fact]
    public async Task GetCertificatesAsync_RepositoryThrowsException_ReturnsFailResponse()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Database connection failure", result.Message);
    }

    [Fact]
    public async Task GetCertificatesAsync_StatusCountsThrowsException_ReturnsFailResponse()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<CoachCertificate>(), 0));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Count query failed"));

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Count query failed", result.Message);
    }

    [Fact]
    public async Task GetCertificatesAsync_CancelledToken_ReturnsFailResponse()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        var result = await _service.GetCertificatesAsync(query, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Error retrieving coach certificates", result.Message);
    }
}

