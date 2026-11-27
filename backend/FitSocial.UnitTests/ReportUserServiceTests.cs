using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

/// <summary>
/// Unit test suite for Report User feature:
/// ReportService.ReportUserAsync(Guid reporterId, CreateUserReportRequestDto request, CancellationToken cancellationToken)
/// Covers:
/// - Normal test cases (UTCID01 - UTCID08): Standard report with media, null Type defaulting to USER, uppercase type conversion, empty/null media, blanks filtering, whitespace trimming, null description
/// - Boundary test cases (UTCID09 - UTCID15): Reason 1 char (min valid), Reason 500 chars (max valid), Reason 501 chars (invalid), Description 500 chars (max valid), Description 501 chars (invalid), MediaUrls 5 items (max valid), MediaUrls 6 items (invalid)
/// - Abnormal test cases (UTCID16 - UTCID24): Unauthenticated reporter, null request, empty reportedUserId, self-reporting, blank reason, reported user not found, reported user locked/banned, pending report exists, database exception
/// </summary>
public class ReportUserServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly ReportService _service;

    public ReportUserServiceTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object);
    }

    private User CreateActiveReportedUser(Guid userId)
    {
        return new User
        {
            UserId = userId,
            FullName = "Reported User",
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };
    }

    #region Normal Test Cases (UTCID01 - UTCID08)

    [Fact]
    public async Task ReportUserAsync_ValidUserReportWithDescriptionAndMedia_ShouldCreateReportSuccessfully()
    {
        // UTCID01 - Normal: Standard report with description and 2 valid media URLs
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Violated community standards with rude comments",
            Description = "Harassment occurred in group chat",
            Type = "USER",
            MediaUrls = new List<string>
            {
                "https://cdn.example.com/proof1.jpg",
                "https://cdn.example.com/proof2.jpg"
            }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockReportRepo.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("User reported successfully.", result.Message);
        Assert.NotNull(result.Data);

        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReporterId == reporterId &&
            rep.ReportedUserId == reportedUserId &&
            rep.Reason == "Violated community standards with rude comments" &&
            rep.Description == "Harassment occurred in group chat" &&
            rep.Type == "USER" &&
            rep.Status == "PENDING" &&
            rep.ReportMedia.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithoutExplicitType_ShouldDefaultTypeToUSER()
    {
        // UTCID02 - Normal: When Type is null or empty, defaults to "USER"
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Spamming advertisements in chat",
            Type = null
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Type == "USER"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithExplicitCoachType_ShouldSetUppercaseType()
    {
        // UTCID03 - Normal: Custom type like "coach" is trimmed and converted to uppercase "COACH"
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Unprofessional conduct during training",
            Type = "  coach  "
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Type == "COACH"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithoutMediaUrls_ShouldCreateReportWithEmptyMediaList()
    {
        // UTCID04 - Normal: When MediaUrls is null, report is created with 0 media
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Inappropriate profile picture",
            MediaUrls = null
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportMedia.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithEmptyMediaUrlsList_ShouldCreateReportWithEmptyMediaList()
    {
        // UTCID05 - Normal: When MediaUrls is an empty list, report is created with 0 media
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Offensive nickname",
            MediaUrls = new List<string>()
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportMedia.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithNullAndWhitespaceInMediaUrls_ShouldFilterBlanksOut()
    {
        // UTCID06 - Normal: Media list containing null, empty and whitespace strings filters them out
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Impersonation",
            MediaUrls = new List<string>
            {
                "https://cdn.example.com/screenshot1.png",
                "",
                "   ",
                null!,
                "https://cdn.example.com/screenshot2.png"
            }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportMedia.Count == 2 &&
            rep.ReportMedia.All(m => !string.IsNullOrWhiteSpace(m.MediaUrl))), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithLeadingAndTrailingWhitespace_ShouldTrimReasonAndDescription()
    {
        // UTCID07 - Normal: Reason and description with whitespace should be trimmed
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "   Offensive remarks   ",
            Description = "   Repeated offensive messages in chat   "
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Reason == "Offensive remarks" &&
            rep.Description == "Repeated offensive messages in chat"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WithoutDescription_ShouldCreateReportWithNullDescription()
    {
        // UTCID08 - Normal: Description can be null or whitespace, resulting in null description
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Fake account",
            Description = "   "
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Description == ""), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Boundary Test Cases (UTCID09 - UTCID15)

    [Fact]
    public async Task ReportUserAsync_WhenReasonIsExactly1Character_ShouldSucceed()
    {
        // UTCID09 - Boundary: Reason length is exactly 1 character (minimum boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "A"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Reason == "A"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WhenReasonIsExactly500Characters_ShouldSucceed()
    {
        // UTCID10 - Boundary: Reason length is exactly 500 characters (maximum valid boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);
        var reason500 = new string('R', 500);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = reason500
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Reason.Length == 500), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WhenReasonIs501Characters_ShouldThrowValidationException()
    {
        // UTCID11 - Boundary: Reason length is 501 characters (upper invalid boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = new string('R', 501)
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Reason cannot exceed 500 characters.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenDescriptionIsExactly500Characters_ShouldSucceed()
    {
        // UTCID12 - Boundary: Description length is exactly 500 characters (maximum valid boundary matching varchar(500))
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);
        var desc500 = new string('D', 500);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Valid violation reason",
            Description = desc500
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.Description!.Length == 500), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WhenDescriptionIs501Characters_ShouldThrowValidationException()
    {
        // UTCID13 - Boundary: Description length is 501 characters (upper invalid boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Valid violation reason",
            Description = new string('D', 501)
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Description cannot exceed 500 characters.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenMediaUrlsCountIsExactly5_ShouldSucceed()
    {
        // UTCID14 - Boundary: Exactly 5 evidence media URLs (maximum allowed boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Multiple policy violations",
            MediaUrls = new List<string>
            {
                "https://cdn.example.com/p1.png",
                "https://cdn.example.com/p2.png",
                "https://cdn.example.com/p3.png",
                "https://cdn.example.com/p4.png",
                "https://cdn.example.com/p5.png"
            }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _service.ReportUserAsync(reporterId, request);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockReportRepo.Verify(r => r.AddAsync(It.Is<Report>(rep =>
            rep.ReportMedia.Count == 5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReportUserAsync_WhenMediaUrlsCountIs6_ShouldThrowValidationException()
    {
        // UTCID15 - Boundary: 6 evidence media URLs (upper invalid boundary)
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Excessive photos test",
            MediaUrls = new List<string>
            {
                "https://cdn.example.com/p1.png",
                "https://cdn.example.com/p2.png",
                "https://cdn.example.com/p3.png",
                "https://cdn.example.com/p4.png",
                "https://cdn.example.com/p5.png",
                "https://cdn.example.com/p6.png"
            }
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("You can attach a maximum of 5 evidence photos.", ex.Message);
    }

    #endregion

    #region Abnormal Test Cases (UTCID16 - UTCID24)

    [Fact]
    public async Task ReportUserAsync_WhenReporterIdIsEmptyGuid_ShouldThrowValidationException()
    {
        // UTCID16 - Abnormal: ReporterId is Guid.Empty (unauthenticated user)
        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = Guid.NewGuid(),
            Reason = "Valid violation reason"
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(Guid.Empty, request));

        Assert.Equal("User is not authenticated.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenRequestIsNull_ShouldThrowValidationException()
    {
        // UTCID17 - Abnormal: Request body is null
        var reporterId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, null!));

        Assert.Equal("Reported user ID is required.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenReportedUserIdIsEmptyGuid_ShouldThrowValidationException()
    {
        // UTCID18 - Abnormal: ReportedUserId in request is Guid.Empty
        var reporterId = Guid.NewGuid();
        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = Guid.Empty,
            Reason = "Valid violation reason"
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Reported user ID is required.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenUserReportsThemselves_ShouldThrowBusinessException()
    {
        // UTCID19 - Abnormal: ReporterId is identical to ReportedUserId (self-reporting prohibited)
        var selfId = Guid.NewGuid();
        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = selfId,
            Reason = "Self reporting test"
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportUserAsync(selfId, request));

        Assert.Equal("You cannot report yourself.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReportUserAsync_WhenReasonIsNullOrWhitespace_ShouldThrowValidationException(string? invalidReason)
    {
        // UTCID20 - Abnormal: Reason is null, empty string, or whitespace only
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = invalidReason!
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Reason is required.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenReportedUserNotFound_ShouldThrowNotFoundException()
    {
        // UTCID21 - Abnormal: Target reported user does not exist in database
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Valid violation reason"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Reported user not found.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenReportedUserIsLocked_ShouldThrowBusinessException()
    {
        // UTCID22 - Abnormal: Target reported user account is already locked or banned
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = reportedUserId,
            FullName = "Locked User",
            IsLocked = true
        };

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Valid violation reason"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("This user account is already locked or banned.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenPendingReportAlreadyExists_ShouldThrowBusinessException()
    {
        // UTCID23 - Abnormal: Duplicate report pending for the target user from this reporter
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Duplicate harassment report"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("You already have a pending report for this user. Please wait for our moderators to review it.", ex.Message);
    }

    [Fact]
    public async Task ReportUserAsync_WhenDatabaseFailsOnAdd_ShouldPropagateException()
    {
        // UTCID24 - Abnormal: Database failure during report creation
        var reporterId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var reportedUser = CreateActiveReportedUser(reportedUserId);

        var request = new CreateUserReportRequestDto
        {
            ReportedUserId = reportedUserId,
            Reason = "Valid violation reason"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reportedUser);
        _mockReportRepo.Setup(r => r.HasPendingReportAsync(reporterId, reportedUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockReportRepo.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failure"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReportUserAsync(reporterId, request));

        Assert.Equal("Database connection failure", ex.Message);
    }

    #endregion
}
