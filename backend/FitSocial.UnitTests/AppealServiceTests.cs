using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Appeals;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class AppealServiceTests
{
    private readonly Mock<IAppealRepository> _mockAppealRepo;
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<AppealService>> _mockLogger;
    private readonly AppealService _service;

    public AppealServiceTests()
    {
        _mockAppealRepo = new Mock<IAppealRepository>();
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AppealService>>();

        _mockConfig.Setup(c => c["Jwt:SecretKey"]).Returns("ThisIsAStrongSecretKeyForUnitTestsAppealService1234567890!");

        _service = new AppealService(
            _mockAppealRepo.Object,
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object,
            _mockConfig.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SubmitAppealAsync_ValidRequest_CreatesPendingAppealAndReturnsDetail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = userId,
            Reason = "Inappropriate profile language",
            Status = "ACTIONTAKEN",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var user = new User
        {
            UserId = userId,
            Email = "trainee@example.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockAppealRepo.Setup(a => a.HasActiveAppealForReportAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Misunderstanding of profile bio",
            Content = "The text cited was an exercise motivational quote and not offensive.",
            EvidenceUrls = new List<string> { "https://res.cloudinary.com/evidence1.jpg" }
        };

        // Act
        var result = await _service.SubmitAppealAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(reportId, result.Data.ReportId);
        Assert.Equal(userId, result.Data.AppealerId);
        Assert.Equal("PENDING", result.Data.Status);
        Assert.Equal(request.Reason, result.Data.Reason);
        Assert.Equal(request.Content, result.Data.Content);
        Assert.Single(result.Data.Media);
        Assert.Equal("https://res.cloudinary.com/evidence1.jpg", result.Data.Media[0].MediaUrl);

        _mockAppealRepo.Verify(a => a.AddAsync(It.Is<Appeal>(ap => ap.ReportId == reportId && ap.AppealerId == userId && ap.Status == "PENDING"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAppealAsync_ReportNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
            Email = "trainee@example.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync((Report?)null);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Reason",
            Content = "Detailed explanation"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.SubmitAppealAsync(userId, request));
    }

    [Fact]
    public async Task SubmitAppealAsync_UserNotReportTarget_ThrowsForbiddenException()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var user = new User
        {
            UserId = callerUserId,
            Email = "trainee@example.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = anotherUserId, // Different user
            ReportedPostId = null,
            Status = "ACTIONTAKEN"
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(callerUserId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Reason",
            Content = "Detailed explanation"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _service.SubmitAppealAsync(callerUserId, request));
        Assert.Contains("only appeal reports that were filed against your own account", ex.Message);
    }

    [Fact]
    public async Task SubmitAppealAsync_ActiveAppealAlreadyExists_ThrowsBusinessException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
            Email = "trainee@example.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee
        };

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = userId,
            Status = "ACTIONTAKEN"
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockAppealRepo.Setup(a => a.HasActiveAppealForReportAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Reason",
            Content = "Detailed explanation"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _service.SubmitAppealAsync(userId, request));
        Assert.Contains("already been submitted for this report", ex.Message);
    }

    [Fact]
    public void GenerateAppealToken_And_ValidateAppealToken_RoundTrip_Succeeds()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "blocked@example.com",
            FullName = "Blocked User",
            RoleCode = RoleConstants.Trainee,
            WarningCount = 3
        };

        // Act
        var token = _service.GenerateAppealToken(user);
        var parsedUserId = _service.ValidateAppealToken(token);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(user.UserId, parsedUserId);
    }

    [Fact]
    public void ValidateAppealToken_TamperedToken_ThrowsValidationException()
    {
        // Arrange
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "user@example.com",
            FullName = "Test User",
            RoleCode = RoleConstants.Coach,
            WarningCount = 3
        };

        var token = _service.GenerateAppealToken(user);
        var tamperedToken = token.Substring(0, token.Length - 4) + "XXXX";

        // Act & Assert
        Assert.Throws<ValidationException>(() => _service.ValidateAppealToken(tamperedToken));
    }

    [Fact]
    public async Task SubmitAppealAsync_WithValidAppealToken_SucceedsWithoutLoggedInClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var user = new User
        {
            UserId = userId,
            Email = "blocked@example.com",
            FullName = "Blocked User",
            RoleCode = RoleConstants.Trainee,
            WarningCount = 3
        };

        var token = _service.GenerateAppealToken(user);

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = userId,
            Status = "ACTIONTAKEN"
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockAppealRepo.Setup(a => a.HasActiveAppealForReportAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Appeal from blocked account",
            Content = "Here is my full explanation for the blocked account.",
            AppealToken = token
        };

        // Act: passing null currentUserId simulates anonymous request with AppealToken
        var result = await _service.SubmitAppealAsync(null, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.AppealerId);
        Assert.Equal("PENDING", result.Data.Status);
    }

    [Fact]
    public async Task GetAppealDetailAsync_OtherUserAccess_ThrowsForbiddenException()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var appealerUserId = Guid.NewGuid();
        var appealId = Guid.NewGuid();

        var appeal = new Appeal
        {
            AppealId = appealId,
            AppealerId = appealerUserId, // Belongs to someone else
            Status = "PENDING"
        };

        _mockAppealRepo.Setup(a => a.GetByIdWithDetailsAsync(appealId, It.IsAny<CancellationToken>())).ReturnsAsync(appeal);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetAppealDetailAsync(appealId, callerUserId, null));
    }

    [Fact]
    public async Task SubmitAppealAsync_VideoAndImageEvidence_SetsCorrectMediaTypes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = userId,
            Reason = "Violation reason",
            Status = "ACTIONTAKEN"
        };

        var user = new User
        {
            UserId = userId,
            Email = "coach@example.com",
            FullName = "Coach User",
            RoleCode = RoleConstants.Coach
        };

        _mockUserRepo.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);
        _mockAppealRepo.Setup(a => a.HasActiveAppealForReportAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new SubmitAppealRequestDto
        {
            ReportId = reportId,
            Reason = "Misunderstanding of post",
            Content = "Video evidence proving no rules were broken during workout session.",
            EvidenceUrls = new List<string>
            {
                "https://res.cloudinary.com/fitsocial/posts/images/screenshot.png",
                "https://res.cloudinary.com/fitsocial/posts/videos/proof.mp4"
            }
        };

        // Act
        var result = await _service.SubmitAppealAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Media.Count);

        var imageMedia = result.Data.Media.Find(m => m.MediaUrl.EndsWith(".png"));
        var videoMedia = result.Data.Media.Find(m => m.MediaUrl.EndsWith(".mp4"));

        Assert.NotNull(imageMedia);
        Assert.Equal("IMAGE", imageMedia.MediaType);

        Assert.NotNull(videoMedia);
        Assert.Equal("VIDEO", videoMedia.MediaType);

        _mockAppealRepo.Verify(a => a.AddAsync(It.Is<Appeal>(ap =>
            ap.AppealMedia.Count == 2 &&
            ap.AppealMedia.Any(m => m.MediaType == "VIDEO" && m.MediaUrl == "https://res.cloudinary.com/fitsocial/posts/videos/proof.mp4") &&
            ap.AppealMedia.Any(m => m.MediaType == "IMAGE" && m.MediaUrl == "https://res.cloudinary.com/fitsocial/posts/images/screenshot.png")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportForAppealAsync_OtherUserAccess_ThrowsForbiddenException()
    {
        // Arrange
        var callerUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = anotherUserId, // Not the caller
            Status = "ACTIONTAKEN"
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetReportForAppealAsync(reportId, callerUserId, null));
    }
}
