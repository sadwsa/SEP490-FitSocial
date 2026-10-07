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
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UserService _userService;

    public StaffAdminProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userService = new UserService(_userRepoMock.Object, _unitOfWorkMock.Object);
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

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenAdmin_UpdatesProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            Email = "admin@fitsocial.com",
            PhoneNumber = "0901234567",
            FullName = "Old Admin Name",
            RoleCode = RoleConstants.Admin,
            AvatarUrl = "https://fitsocial.com/old-avatar.jpg",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "Male",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync(It.IsAny<string>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "New Admin Name",
            PhoneNumber = "0987654321",
            Gender = "Female",
            DateOfBirth = new DateOnly(1992, 5, 10),
            Avatar = "https://fitsocial.com/new-avatar.jpg"
        };

        // Act
        var result = await _userService.UpdateStaffAdminProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("New Admin Name", result.Data?.FullName);
        Assert.Equal("0987654321", result.Data?.PhoneNumber);
        Assert.Equal("Female", result.Data?.Gender);
        Assert.Equal(new DateOnly(1992, 5, 10), result.Data?.DateOfBirth);
        Assert.Equal("https://fitsocial.com/new-avatar.jpg", result.Data?.Avatar);
        Assert.Equal(RoleConstants.Admin, result.Data?.Role);

        _userRepoMock.Verify(r => r.Update(adminUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenStaff_UpdatesProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffUser = new User
        {
            UserId = userId,
            Email = "staff@fitsocial.com",
            PhoneNumber = "0911222333",
            FullName = "Staff Tran",
            RoleCode = RoleConstants.Staff,
            AvatarUrl = "https://fitsocial.com/staff.jpg",
            DateOfBirth = new DateOnly(1995, 8, 20),
            Gender = "Male",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync(It.IsAny<string>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "Staff Tran Updated",
            PhoneNumber = "0999888777",
            Gender = "Other",
            DateOfBirth = new DateOnly(1996, 9, 21),
            Avatar = "https://fitsocial.com/staff-updated.jpg"
        };

        // Act
        var result = await _userService.UpdateStaffAdminProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Staff Tran Updated", result.Data?.FullName);
        Assert.Equal("0999888777", result.Data?.PhoneNumber);
        Assert.Equal("Other", result.Data?.Gender);
        Assert.Equal(new DateOnly(1996, 9, 21), result.Data?.DateOfBirth);
        Assert.Equal("https://fitsocial.com/staff-updated.jpg", result.Data?.Avatar);
        Assert.Equal(RoleConstants.Staff, result.Data?.Role);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenFieldsEmptyOrNull_RetainsExistingValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            Email = "admin@fitsocial.com",
            PhoneNumber = "0901234567",
            FullName = "Original Admin",
            RoleCode = RoleConstants.Admin,
            AvatarUrl = "https://fitsocial.com/original-avatar.jpg",
            DateOfBirth = new DateOnly(1988, 3, 15),
            Gender = "Male",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // All fields empty, whitespace, or null
        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "",
            PhoneNumber = "   ",
            Gender = null,
            DateOfBirth = null,
            Avatar = null
        };

        // Act
        var result = await _userService.UpdateStaffAdminProfileAsync(userId, request);

        // Assert: all existing values must be retained
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Original Admin", result.Data?.FullName);
        Assert.Equal("0901234567", result.Data?.PhoneNumber);
        Assert.Equal("Male", result.Data?.Gender);
        Assert.Equal(new DateOnly(1988, 3, 15), result.Data?.DateOfBirth);
        Assert.Equal("https://fitsocial.com/original-avatar.jpg", result.Data?.Avatar);

        _userRepoMock.Verify(r => r.Update(adminUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenFullNameHasNumbers_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Admin,
            FullName = "Admin Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "Admin123"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
        Assert.Contains("numbers", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenPhoneFormatInvalid_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff,
            FullName = "Staff Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            PhoneNumber = "09123" // Invalid: not 10 digits
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
        Assert.Contains("10-digit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenPhoneAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff,
            FullName = "Staff Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync("0909999888", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            PhoneNumber = "0909999888"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenDobInFuture_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staffUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff,
            FullName = "Staff Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "Admin Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Admin,
            FullName = "Locked Admin",
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "Admin Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenNotStaffOrAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Trainee User",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            FullName = "Attempted Update"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.UpdateStaffAdminProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateStaffAdminProfileAsync_WhenAvatarExplicitlyRemoved_ClearsAvatarUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Admin,
            FullName = "Admin With Avatar",
            AvatarUrl = "https://fitsocial.com/current-avatar.jpg",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        var request = new UpdateStaffAdminProfileRequestDto
        {
            Avatar = "[REMOVE]"
        };

        // Act
        var result = await _userService.UpdateStaffAdminProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Null(result.Data?.Avatar);
        Assert.Null(adminUser.AvatarUrl);
    }
}
