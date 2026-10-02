using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.Services;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class AdminUserServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _service = new AdminUserService(_userRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task SetUserLockStatusAsync_LockUser_ShouldIncrementTokenVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "violator@example.com",
            IsLocked = false,
            TokenVersion = 1
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.SetUserLockStatusAsync(userId, isLocked: true, adminId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(user.IsLocked);
        Assert.Equal(adminId, user.LockedBy);
        Assert.Equal(2, user.TokenVersion); // Incremented from 1 to 2
    }

    [Fact]
    public async Task WarnUserAsync_FirstWarning_ShouldIncrementWarningCountOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "warned@example.com",
            IsLocked = false,
            WarningCount = 0,
            TokenVersion = 1
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.WarnUserAsync(userId, "First community violation");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, user.WarningCount);
        Assert.False(user.IsLocked);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task WarnUserAsync_ThirdWarning_ShouldAutoLockAndInvalidateTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var user = new User
        {
            UserId = userId,
            Email = "repeat.violator@example.com",
            IsLocked = false,
            WarningCount = 2,
            TokenVersion = 1
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.WarnUserAsync(userId, "Third strike - inappropriate content", staffId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(3, user.WarningCount);
        Assert.True(user.IsLocked);
        Assert.Equal(staffId, user.LockedBy);
        Assert.Equal(2, user.TokenVersion); // Auto-lock bumps TokenVersion from 1 to 2
    }

    [Fact]
    public async Task GetStaffUsersAsync_ShouldCallListStaffForAdminAsync_AndReturnPagedStaffAccounts()
    {
        // Arrange
        var staffUsers = new System.Collections.Generic.List<User>
        {
            new()
            {
                UserId = Guid.NewGuid(),
                FullName = "Staff One",
                Email = "staff1@fitsocial.com",
                PhoneNumber = "0901234567",
                RoleCode = "STAFF",
                IsLocked = false,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                UserId = Guid.NewGuid(),
                FullName = "Staff Two",
                Email = "staff2@fitsocial.com",
                PhoneNumber = "0909876543",
                RoleCode = "STAFF",
                IsLocked = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        _userRepoMock.Setup(r => r.ListStaffForAdminAsync(
                null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((staffUsers, 2));

        // Act
        var result = await _service.GetStaffUsersAsync(null, null, 1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.All(result.Data.Items, item => Assert.Equal("STAFF", item.RoleCode));
        Assert.Equal("Staff One", result.Data.Items[0].FullName);
        Assert.False(result.Data.Items[0].IsLocked);
        Assert.Equal("Staff Two", result.Data.Items[1].FullName);
        Assert.True(result.Data.Items[1].IsLocked);
    }

    [Fact]
    public async Task GetStaffUsersAsync_WithSearchAndLockedFilter_ShouldPassParametersCorrectly()
    {
        // Arrange
        var staffUsers = new System.Collections.Generic.List<User>
        {
            new()
            {
                UserId = Guid.NewGuid(),
                FullName = "Active Staff",
                Email = "active.staff@fitsocial.com",
                RoleCode = "STAFF",
                IsLocked = false
            }
        };

        _userRepoMock.Setup(r => r.ListStaffForAdminAsync(
                "active", false, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((staffUsers, 1));

        // Act
        var result = await _service.GetStaffUsersAsync("active", false, 2, 5);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("Active Staff", result.Data.Items[0].FullName);
        _userRepoMock.Verify(r => r.ListStaffForAdminAsync(
            "active", false, 2, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStaffUsersAsync_WhenRepositoryThrows_ShouldReturnFailureResult()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ListStaffForAdminAsync(
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failure"));

        // Act
        var result = await _service.GetStaffUsersAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Database connection failure", result.Message);
    }
}
