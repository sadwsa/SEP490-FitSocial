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
    private readonly Mock<IAuditLogRepository> _mockAuditRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly CoachCertificateService _service;

    public ViewCoachCertificateListServiceTests()
    {
        _mockCertRepo = new Mock<ICoachCertificateRepository>();
        _mockCoachRepo = new Mock<ICoachProfileRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockAuditRepo = new Mock<IAuditLogRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();

        _service = new CoachCertificateService(
            _mockCertRepo.Object,
            _mockCoachRepo.Object,
            _mockUow.Object,
            _mockAuditRepo.Object,
            _mockNotificationRepo.Object);
    }

    private static CoachCertificate CreateCertificate(
        Guid? id = null,
        Guid? coachId = null,
        string status = "PENDING",
        string coachName = "Coach John Doe",
        string coachEmail = "coach@example.com",
        bool includeCoach = true)
    {
        var certId = id ?? Guid.NewGuid();
        var coId = coachId ?? Guid.NewGuid();

        return new CoachCertificate
        {
            CertificateId = certId,
            CoachId = coId,
            CertificateName = "Certified Personal Trainer",
            CertificateUrl = "https://example.com/cert.pdf",
            IssuedBy = "American Council on Exercise",
            IssuedDate = new DateOnly(2023, 1, 1),
            ExpiryDate = new DateOnly(2025, 1, 1),
            VerificationStatus = status,
            CreatedAt = DateTime.UtcNow,
            Coach = includeCoach
                ? new CoachProfile
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
                : null!
        };
    }

    #region Normal Cases (N)

    /// <summary>
    /// UTCID01 - GetCertificatesAsync_DefaultQuery_ReturnsPagedCertificatesWithStatusCounts
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID02 - GetCertificatesAsync_FilterByPendingStatus_ReturnsFilteredPendingCertificates
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "PENDING"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_FilterByPendingStatus_ReturnsFilteredPendingCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "PENDING" };
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
        Assert.Equal(1, result.Data.Certificates.TotalCount);
    }

    /// <summary>
    /// UTCID03 - GetCertificatesAsync_FilterByApprovedStatus_ReturnsFilteredApprovedCertificates
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "APPROVED"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_FilterByApprovedStatus_ReturnsFilteredApprovedCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "APPROVED" };
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
        Assert.Equal(1, result.Data.Certificates.TotalCount);
    }

    /// <summary>
    /// UTCID04 - GetCertificatesAsync_FilterByRejectedStatus_ReturnsFilteredRejectedCertificates
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "REJECTED"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_FilterByRejectedStatus_ReturnsFilteredRejectedCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Status = "REJECTED" };
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
        Assert.Equal(1, result.Data.Certificates.TotalCount);
    }

    /// <summary>
    /// UTCID05 - GetCertificatesAsync_FilterBySearchKeyword_ReturnsMatchingCertificates
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "ALL"
    /// - Search: "NASM"
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_FilterBySearchKeyword_ReturnsMatchingCertificates()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { Search = "NASM", Status = "ALL" };
        var cert = CreateCertificate();
        cert.CertificateName = "NASM Certified Personal Trainer";
        var list = new List<CoachCertificate> { cert };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync("NASM", "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["ALL"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("NASM Certified Personal Trainer", result.Data.Certificates.Items[0].CertificateName);
        Assert.Equal(1, result.Data.Certificates.TotalCount);
    }

    /// <summary>
    /// UTCID06 - GetCertificatesAsync_CustomSortBy_PassesSortParameterAndReturnsResults
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_asc"
    /// - Coach: Found
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_CustomSortBy_PassesSortParameterAndReturnsResults()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto { SortBy = "date_asc" };
        var list = new List<CoachCertificate> { CreateCertificate() };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_asc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["ALL"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockCertRepo.Verify(r => r.GetPagedCertificatesAsync(null, "ALL", "date_asc", 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID07 - GetCertificatesAsync_WithNullCoachNavigation_MapsWithDefaultUnknownCoach
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: empty/null
    /// - DatabaseResults: Found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: N
    /// Status: Passed
    /// </summary>
    [Fact]
    public async Task GetCertificatesAsync_WithNullCoachNavigation_MapsWithDefaultUnknownCoach()
    {
        // Arrange
        var query = new GetCoachCertificatesAdminQueryDto();
        var certWithoutCoach = CreateCertificate(includeCoach: false);
        var list = new List<CoachCertificate> { certWithoutCoach };

        _mockCertRepo.Setup(r => r.GetPagedCertificatesAsync(null, "ALL", "date_desc", 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((list, 1));

        _mockCertRepo.Setup(r => r.GetStatusCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["ALL"] = 1 });

        // Act
        var result = await _service.GetCertificatesAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data.Certificates.Items);
        Assert.Equal("Unknown Coach", result.Data.Certificates.Items[0].CoachName);
        Assert.Equal(string.Empty, result.Data.Certificates.Items[0].CoachEmail);
    }

    #endregion

    #region Boundary Cases (B)

    /// <summary>
    /// UTCID08 - GetCertificatesAsync_EmptyResult_ReturnsEmptyPagedListWithZeroCounts
    /// Condition:
    /// - Precondition: Database connection is active
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: Found
    /// - DatabaseResults: Not found
    /// Expected: Return ApiResponseDto<CoachCertificateAdminListResponseDto> with Success=true
    /// Type: B
    /// Status: Passed
    /// </summary>
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
        Assert.Equal(0, result.Data.StatusCounts.All);
        Assert.Equal(0, result.Data.StatusCounts.Pending);
        Assert.Equal(0, result.Data.StatusCounts.Approved);
        Assert.Equal(0, result.Data.StatusCounts.Rejected);
    }

    #endregion

    #region Abnormal Cases (A)

    /// <summary>
    /// UTCID09 - GetCertificatesAsync_RepositoryThrowsException_ReturnsFailResponse
    /// Condition:
    /// - Precondition: Database connection failure
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Exception ("Database connection failure")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID10 - GetCertificatesAsync_StatusCountsThrowsException_ReturnsFailResponse
    /// Condition:
    /// - Precondition: Database connection failure
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: empty/null
    /// - DatabaseResults: Not found
    /// Expected: Exception ("Count query failed")
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    /// <summary>
    /// UTCID11 - GetCertificatesAsync_CancelledToken_ReturnsFailResponse
    /// Condition:
    /// - Precondition: Cancelled token
    /// - Status: "ALL"
    /// - Search: empty/null
    /// - SortBy: "date_desc"
    /// - Coach: empty/null
    /// - DatabaseResults: Not found
    /// Expected: OperationCanceledException
    /// Type: A
    /// Status: Passed
    /// </summary>
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

    #endregion
}
