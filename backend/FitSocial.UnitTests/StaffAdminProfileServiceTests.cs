using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class StaffAdminProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly UserService _userService;

    public StaffAdminProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _userService = new UserService(_userRepoMock.Object);
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenStaff_ReturnsCorrectProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffUser = new User
        {
            UserId = userId,
            Email = "staff@fitsocial.com",
            PhoneNumber = "0987654321",
            FullName = "Nguyen Van Staff",
            RoleCode = RoleConstants.Staff,
            AvatarUrl = "https://fitsocial.com/avatars/staff.png",
            DateOfBirth = new DateOnly(1996, 5, 15),
            Gender = "Male",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);

        // Act
        var result = await _userService.GetStaffAdminProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var data = result.Data;
        Assert.Equal("https://fitsocial.com/avatars/staff.png", data.Avatar);
        Assert.Equal("Nguyen Van Staff", data.FullName);
        Assert.Equal(new DateOnly(1996, 5, 15), data.DateOfBirth);
        Assert.Equal("Male", data.Gender);
        Assert.Equal("0987654321", data.PhoneNumber);
        Assert.Equal("staff@fitsocial.com", data.Email);
        Assert.Equal(RoleConstants.Staff, data.Role);
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenAdmin_ReturnsCorrectProfile()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            Email = "admin@fitsocial.com",
            PhoneNumber = "0912345678",
            FullName = "Admin Quản Trị",
            RoleCode = RoleConstants.Admin,
            AvatarUrl = "https://fitsocial.com/avatars/admin.png",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Female",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Act
        var result = await _userService.GetStaffAdminProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var data = result.Data;
        Assert.Equal("https://fitsocial.com/avatars/admin.png", data.Avatar);
        Assert.Equal("Admin Quản Trị", data.FullName);
        Assert.Equal(new DateOnly(1990, 1, 1), data.DateOfBirth);
        Assert.Equal("Female", data.Gender);
        Assert.Equal("0912345678", data.PhoneNumber);
        Assert.Equal("admin@fitsocial.com", data.Email);
        Assert.Equal(RoleConstants.Admin, data.Role);
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetStaffAdminProfileAsync(userId));
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenAccountIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedStaff = new User
        {
            UserId = userId,
            Email = "lockedstaff@fitsocial.com",
            FullName = "Locked Staff",
            RoleCode = RoleConstants.Staff,
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedStaff);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.GetStaffAdminProfileAsync(userId));
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenUserIsTrainee_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            Email = "trainee@fitsocial.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.GetStaffAdminProfileAsync(userId));
    }

    [Fact]
    public async Task GetStaffAdminProfileAsync_WhenUserIsCoach_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            Email = "coach@fitsocial.com",
            FullName = "Coach User",
            RoleCode = RoleConstants.Coach,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.GetStaffAdminProfileAsync(userId));
    }
}
