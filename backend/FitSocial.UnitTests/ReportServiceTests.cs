using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FitSocial.UnitTests;

public class ReportServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _mockReportRepo = new Mock<IReportRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow = new Mock<IUnitOfWork>();

        _service = new ReportService(
            _mockReportRepo.Object,
            _mockUserRepo.Object,
            _mockUow.Object);
    }

    [Fact]
    public async Task ResolveReportAsync_ShouldSetStatusAndResolverInfo()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var report = new Report
        {
            ReportId = reportId,
            Status = "PENDING",
            Reason = "Inappropriate content"
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new ResolveReportRequestDto { Status = "RESOLVED", ResolutionNote = "Content removed" };

        // Act
        var result = await _service.ResolveReportAsync(reportId, staffId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("RESOLVED", report.Status);
        Assert.Equal(staffId, report.ResolvedBy);
        Assert.NotNull(report.ResolvedAt);
        Assert.NotNull(report.NotifiedReportedUserAt);
    }

    [Fact]
    public async Task SubmitAppealAsync_WhenReportedUserAppeals_ShouldSetPendingAppealAndMedia()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var reportedUserId = Guid.NewGuid();
        var report = new Report
        {
            ReportId = reportId,
            ReportedUserId = reportedUserId,
            Status = "RESOLVED"
        };

        _mockReportRepo.Setup(r => r.GetReportWithDetailsByIdAsync(reportId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new SubmitAppealRequestDto
        {
            AppealContent = "I did not violate any terms. Please re-check the post context.",
            MediaUrls = new List<string> { "https://cdn.example.com/appeal-proof.jpg" }
        };

        // Act
        var result = await _service.SubmitAppealAsync(reportId, reportedUserId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("PENDING", report.AppealStatus);
        Assert.Equal(request.AppealContent, report.AppealContent);
        Assert.NotNull(report.AppealedAt);
        Assert.Single(report.ReportMedia);
        Assert.Equal("APPEAL", report.ReportMedia.First().MediaFor);
    }
}
