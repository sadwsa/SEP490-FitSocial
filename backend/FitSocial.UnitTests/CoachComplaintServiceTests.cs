using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FitSocial.UnitTests;

public class CoachComplaintServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IOrderRepository> _mockOrderRepo;
    private readonly Mock<ICoachProfileRepository> _mockCoachProfileRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IPostRepository> _mockPostRepo;
    private readonly ReportService _service;

    public CoachComplaintServiceTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _mockOrderRepo = new Mock<IOrderRepository>();
        _mockCoachProfileRepo = new Mock<ICoachProfileRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockPostRepo = new Mock<IPostRepository>();

        _service = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object,
            _mockOrderRepo.Object,
            _mockCoachProfileRepo.Object,
            _mockNotificationRepo.Object,
            _mockPostRepo.Object);
    }

    [Fact]
    public async Task SubmitCoachComplaint_Unauthenticated_ThrowsValidationException()
    {
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = Guid.NewGuid(),
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(Guid.Empty, request));

        Assert.Contains("authenticated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_NullRequest_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, null!));

        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_EmptyCoachId_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = Guid.Empty,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("Coach is required", ex.Message);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ComplainingAgainstSelf_ThrowsBusinessException()
    {
        var reporterId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = reporterId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("cannot submit a complaint against yourself", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ReporterNotFound_ThrowsNotFoundException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("Reporter account not found", ex.Message);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ReporterLocked_ThrowsBusinessException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = true
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("account is locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ReporterNotTraineeOrCoach_ThrowsBusinessException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Admin, // Admin cannot submit user coach complaints through this flow
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("Only trainees and coaches", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")] // Too short (< 3)
    public async Task SubmitCoachComplaint_InvalidTitle_ThrowsValidationException(string title)
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = title,
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));
    }

    [Fact]
    public async Task SubmitCoachComplaint_TitleTooLong_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = new string('A', 501),
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("500 characters", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")] // Less than 10 characters
    public async Task SubmitCoachComplaint_InvalidContent_ThrowsValidationException(string content)
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = content
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));
    }

    [Fact]
    public async Task SubmitCoachComplaint_ContentTooLong_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = new string('B', 501)
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("500 characters", ex.Message);
    }

    [Fact]
    public async Task SubmitCoachComplaint_CoachNotFound_ThrowsNotFoundException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("Coach not found", ex.Message);
    }

    [Fact]
    public async Task SubmitCoachComplaint_TargetUserNotCoach_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = targetId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var targetUser = new User
        {
            UserId = targetId,
            RoleCode = RoleConstants.Trainee, // Target is trainee, not coach
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("not a coach", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_TargetCoachLocked_ThrowsBusinessException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var coachUser = new User
        {
            UserId = coachId,
            RoleCode = RoleConstants.Coach,
            IsLocked = true // Target coach account is locked
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("inactive or locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_TargetCoachProfileNotFound_ThrowsNotFoundException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var coachUser = new User
        {
            UserId = coachId,
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _mockCoachProfileRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CoachProfile?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("Coach profile not found", ex.Message);
    }

    [Fact]
    public async Task SubmitCoachComplaint_TargetCoachProfileRejected_ThrowsBusinessException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident."
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var coachUser = new User
        {
            UserId = coachId,
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "REJECTED"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _mockCoachProfileRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("not active", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_TooManyEvidenceFiles_ThrowsValidationException()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Valid Complaint Title",
            Content = "Valid complaint description detailing the incident.",
            EvidenceUrls = Enumerable.Range(1, 11).Select(i => $"https://cdn.example.com/file{i}.jpg").ToList()
        };

        var reporter = new User
        {
            UserId = reporterId,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var coachUser = new User
        {
            UserId = coachId,
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "APPROVED"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _mockCoachProfileRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitCoachComplaintAsync(reporterId, request));

        Assert.Contains("maximum of 10 evidence files", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ValidTraineeReporter_CreatesPendingReportWithEvidence()
    {
        var reporterId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = coachId,
            Title = "Missed training appointment",
            Content = "The coach failed to appear for our scheduled 1-on-1 session on Monday without prior notice.",
            EvidenceUrls = new List<string>
            {
                "https://cdn.example.com/chat-screenshot.jpg",
                "https://cdn.example.com/invoice.pdf"
            }
        };

        var reporter = new User
        {
            UserId = reporterId,
            FullName = "Trainee John",
            Email = "john@example.com",
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        var coachUser = new User
        {
            UserId = coachId,
            FullName = "Coach Sarah",
            Email = "sarah@example.com",
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        var coachProfile = new CoachProfile
        {
            CoachId = coachId,
            ApprovalStatus = "APPROVED"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporter);

        _mockUserRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        _mockCoachProfileRepo.Setup(r => r.GetByIdAsync(coachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachProfile);

        Report? capturedReport = null;
        _mockReportRepo.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Callback<Report, CancellationToken>((r, _) => capturedReport = r)
            .Returns(Task.CompletedTask);

        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => capturedReport);

        // Act
        var result = await _service.SubmitCoachComplaintAsync(reporterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedReport);
        Assert.Equal(reporterId, capturedReport.ReporterId);
        Assert.Equal(coachId, capturedReport.ReportedUserId);
        Assert.Null(capturedReport.ReportedPostId);
        Assert.Equal("COACH", capturedReport.Type);
        Assert.Equal("PENDING", capturedReport.Status);
        Assert.Equal("Missed training appointment", capturedReport.Reason);
        Assert.Equal(request.Content, capturedReport.Description);
        Assert.Equal(2, capturedReport.ReportMedia.Count);

        var firstMedia = capturedReport.ReportMedia.First();
        Assert.Equal("https://cdn.example.com/chat-screenshot.jpg", firstMedia.MediaUrl);
        Assert.Equal("IMAGE", firstMedia.MediaType);
        Assert.Equal("COMPLAINT", firstMedia.MediaFor);

        var secondMedia = capturedReport.ReportMedia.Last();
        Assert.Equal("https://cdn.example.com/invoice.pdf", secondMedia.MediaUrl);
        Assert.Equal("PDF", secondMedia.MediaType);
        Assert.Equal("COMPLAINT", secondMedia.MediaFor);

        _mockReportRepo.Verify(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitCoachComplaint_ValidCoachReporter_CreatesPendingReportSuccessfully()
    {
        var reporterId = Guid.NewGuid();
        var targetCoachId = Guid.NewGuid();
        var request = new SubmitCoachComplaintRequestDto
        {
            CoachId = targetCoachId,
            Title = "Unethical client poaching",
            Content = "Target coach was soliciting trainees during our gym facility hours violating agreement."
        };

        var reporterCoach = new User
        {
            UserId = reporterId,
            FullName = "Coach Alice",
            Email = "alice@example.com",
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        var targetCoachUser = new User
        {
            UserId = targetCoachId,
            FullName = "Coach Bob",
            Email = "bob@example.com",
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        var targetCoachProfile = new CoachProfile
        {
            CoachId = targetCoachId,
            ApprovalStatus = "APPROVED"
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync(reporterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reporterCoach);

        _mockUserRepo.Setup(r => r.GetByIdAsync(targetCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetCoachUser);

        _mockCoachProfileRepo.Setup(r => r.GetByIdAsync(targetCoachId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetCoachProfile);

        Report? capturedReport = null;
        _mockReportRepo.Setup(r => r.AddAsync(It.IsAny<Report>(), It.IsAny<CancellationToken>()))
            .Callback<Report, CancellationToken>((r, _) => capturedReport = r)
            .Returns(Task.CompletedTask);

        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => capturedReport);

        // Act
        var result = await _service.SubmitCoachComplaintAsync(reporterId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(capturedReport);
        Assert.Equal(reporterId, capturedReport.ReporterId);
        Assert.Equal(targetCoachId, capturedReport.ReportedUserId);
        Assert.Equal("COACH", capturedReport.Type);
        Assert.Equal("PENDING", capturedReport.Status);
    }
}
