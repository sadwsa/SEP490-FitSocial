using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Interfaces;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class AdminUserServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _service = new AdminUserService(_userRepoMock.Object, _unitOfWorkMock.Object, _passwordHasherMock.Object);
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

    [Fact]
    public async Task CreateStaffAccountAsync_ValidRequest_ShouldCreateStaffWithStaffRoleAndHashedPassword()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "New Staff Member",
            Email = "newstaff@fitsocial.com",
            PhoneNumber = "0987654321",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        _userRepoMock.Setup(r => r.ExistsByEmailAsync("newstaff@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync("0987654321", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasherMock.Setup(p => p.HashPassword("Password123!"))
            .Returns("hashed_pw_secret");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        User? capturedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("newstaff@fitsocial.com", result.Data.Email);
        Assert.Equal("New Staff Member", result.Data.FullName);
        Assert.Equal("STAFF", result.Data.RoleCode);
        Assert.False(result.Data.IsLocked);

        // Verify captured entity
        Assert.NotNull(capturedUser);
        Assert.Equal("newstaff@fitsocial.com", capturedUser.Email);
        Assert.Equal("New Staff Member", capturedUser.FullName);
        Assert.Equal("STAFF", capturedUser.RoleCode);
        Assert.True(capturedUser.IsInternal);
        Assert.Equal("hashed_pw_secret", capturedUser.PasswordHash);
        Assert.Equal(1, capturedUser.TokenVersion);
        Assert.Equal(0, capturedUser.WarningCount);

        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_EmailAlreadyExists_ShouldReturnFailureWithoutCreating()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Duplicate Staff",
            Email = "existing@fitsocial.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        _userRepoMock.Setup(r => r.ExistsByEmailAsync("existing@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Email is already registered", result.Message);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_PasswordMismatch_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Mismatch Staff",
            Email = "mismatch@fitsocial.com",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword!"
        };

        _userRepoMock.Setup(r => r.ExistsByEmailAsync("mismatch@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Passwords do not match", result.Message);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_PhoneAlreadyExists_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Duplicate Phone Staff",
            Email = "phonecheck@fitsocial.com",
            PhoneNumber = "0911222333",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        _userRepoMock.Setup(r => r.ExistsByEmailAsync("phonecheck@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync("0911222333", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Phone number is already registered", result.Message);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_FullNameContainsNumbers_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "John Doe 123",
            Email = "johndoe123@fitsocial.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cannot contain numbers", result.Message, StringComparison.OrdinalIgnoreCase);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_PhoneNumberContainsLetters_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Valid Name",
            Email = "valid@fitsocial.com",
            PhoneNumber = "090123abc",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cannot contain letters", result.Message, StringComparison.OrdinalIgnoreCase);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_PhoneNumberInvalidFormat_ShouldReturnFailure()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Valid Name",
            Email = "valid@fitsocial.com",
            PhoneNumber = "12345",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("valid 10-digit number", result.Message, StringComparison.OrdinalIgnoreCase);
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateStaffAccountAsync_WhenExceptionOccurs_ShouldReturnFailureResult()
    {
        // Arrange
        var request = new CreateStaffAccountRequestDto
        {
            FullName = "Error Staff",
            Email = "error@fitsocial.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        _userRepoMock.Setup(r => r.ExistsByEmailAsync("error@fitsocial.com", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database write error"));

        // Act
        var result = await _service.CreateStaffAccountAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Database write error", result.Message);
    }

    [Fact]
    public async Task CheckEmailExistsAsync_WhenEmailExists_ShouldReturnTrueWithRegisteredMessage()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("registered@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CheckEmailExistsAsync("registered@fitsocial.com");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Email is already registered.", result.Message);
    }

    [Fact]
    public async Task CheckEmailExistsAsync_WhenEmailDoesNotExist_ShouldReturnFalseWithAvailableMessage()
    {
        // Arrange
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("available@fitsocial.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.CheckEmailExistsAsync("available@fitsocial.com");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(result.Data);
        Assert.Equal("Email is available.", result.Message);
    }

    [Fact]
    public async Task CheckEmailExistsAsync_WhenEmailIsEmpty_ShouldReturnFailure()
    {
        // Act
        var result = await _service.CheckEmailExistsAsync("   ");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Email cannot be empty.", result.Message);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_ValidRequest_ShouldUpdateStaffSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingStaff = new User
        {
            UserId = userId,
            Email = "oldemail@fitsocial.com",
            FullName = "Old Staff",
            PhoneNumber = "0901234567",
            RoleCode = RoleConstants.Staff,
            PasswordHash = "old_hash",
            TokenVersion = 1
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Updated Staff",
            Email = "updated@fitsocial.com",
            PhoneNumber = "0987654321",
            Password = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingStaff);
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("updated@fitsocial.com", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync("0987654321", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasherMock.Setup(p => p.HashPassword("NewPassword123!"))
            .Returns("new_hash");
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Updated Staff", result.Data!.FullName);
        Assert.Equal("updated@fitsocial.com", result.Data.Email);
        Assert.Equal("0987654321", result.Data.PhoneNumber);
        Assert.Equal("new_hash", existingStaff.PasswordHash);
        Assert.Equal(2, existingStaff.TokenVersion);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_UserNotFound_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Updated Staff",
            Email = "updated@fitsocial.com"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Staff account not found", result.Message);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_UserNotStaff_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonStaff = new User
        {
            UserId = userId,
            Email = "trainee@fitsocial.com",
            FullName = "Trainee User",
            RoleCode = RoleConstants.Trainee
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Updated Name",
            Email = "updated@fitsocial.com"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(nonStaff);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("User is not a staff member", result.Message);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_FullNameContainsNumbers_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staff = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Staff 123",
            Email = "staff@fitsocial.com"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cannot contain numbers", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_PhoneContainsLetters_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staff = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Staff Member",
            Email = "staff@fitsocial.com",
            PhoneNumber = "090123abc"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("cannot contain letters", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_PhoneInvalidFormat_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staff = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Staff
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Staff Member",
            Email = "staff@fitsocial.com",
            PhoneNumber = "12345"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("valid 10-digit number", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStaffAccountAsync_EmailAlreadyUsedByAnotherUser_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var staff = new User
        {
            UserId = userId,
            Email = "old@fitsocial.com",
            RoleCode = RoleConstants.Staff
        };

        var request = new UpdateStaffAccountRequestDto
        {
            FullName = "Staff Member",
            Email = "conflict@fitsocial.com"
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("conflict@fitsocial.com", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.UpdateStaffAccountAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("Email is already registered", result.Message);
    }
}
