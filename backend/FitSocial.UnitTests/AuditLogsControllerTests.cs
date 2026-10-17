using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.API.Controllers;
using FitSocial.Application.DTOs.AuditLogs;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class AuditLogsControllerTests
{
    private readonly Mock<IAuditLogService> _mockService;
    private readonly AuditLogsController _controller;

    public AuditLogsControllerTests()
    {
        _mockService = new Mock<IAuditLogService>();
        _controller = new AuditLogsController(_mockService.Object);
    }

    [Fact]
    public void AuditLogsController_HasAuthorizeAttribute_WithStaffAndAdminRoles()
    {
        // Act
        var authAttributes = typeof(AuditLogsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        // Assert
        Assert.NotEmpty(authAttributes);
        var roles = authAttributes.First().Roles;
        Assert.NotNull(roles);
        Assert.Contains("STAFF", roles);
        Assert.Contains("ADMIN", roles);
        Assert.DoesNotContain("TRAINEE", roles);
        Assert.DoesNotContain("COACH", roles);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsOk_WhenServiceSucceeds()
    {
        // Arrange
        var pagedResult = PagedResultDto<AuditLogDto>.Create(
            new List<AuditLogDto> { new() { AuditLogId = 1, Action = "APPROVE_REFUND" } },
            1, 1, 10);

        _mockService.Setup(s => s.GetAuditLogsAsync(It.IsAny<GetAuditLogsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<AuditLogDto>>.Ok(pagedResult));

        // Act
        var result = await _controller.GetAuditLogs(new GetAuditLogsQueryDto());

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<PagedResultDto<AuditLogDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsBadRequest_WhenServiceFails()
    {
        // Arrange
        _mockService.Setup(s => s.GetAuditLogsAsync(It.IsAny<GetAuditLogsQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<PagedResultDto<AuditLogDto>>.Fail("Error loading logs"));

        // Act
        var result = await _controller.GetAuditLogs(new GetAuditLogsQueryDto());

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<PagedResultDto<AuditLogDto>>>(badRequestResult.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task GetAuditLogDetail_ReturnsOk_WhenLogExists()
    {
        // Arrange
        var dto = new AuditLogDto { AuditLogId = 123, Action = "LOCK_USER" };
        _mockService.Setup(s => s.GetAuditLogByIdAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<AuditLogDto>.Ok(dto));

        // Act
        var result = await _controller.GetAuditLogDetail(123);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<AuditLogDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(123, response.Data!.AuditLogId);
    }

    [Fact]
    public async Task GetAuditLogDetail_ReturnsNotFound_WhenLogDoesNotExist()
    {
        // Arrange
        _mockService.Setup(s => s.GetAuditLogByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponseDto<AuditLogDto>.Fail("Audit log not found"));

        // Act
        var result = await _controller.GetAuditLogDetail(999);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<AuditLogDto>>(notFoundResult.Value);
        Assert.False(response.Success);
    }
}
