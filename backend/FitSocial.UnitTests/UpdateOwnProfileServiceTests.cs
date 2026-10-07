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

public class UpdateOwnProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UserService _userService;

    public UpdateOwnProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userService = new UserService(_userRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenTrainee_UpdatesProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            Email = "trainee@fitsocial.com",
            PhoneNumber = "0901234567",
            FullName = "Old Trainee Name",
            RoleCode = RoleConstants.Trainee,
            AvatarUrl = "https://fitsocial.com/old.png",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = "Male",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync(It.IsAny<string>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "New Trainee Name",
            PhoneNumber = "0987654321",
            Gender = "Female",
            DateOfBirth = new DateOnly(1999, 12, 31),
            AvatarUrl = "https://fitsocial.com/new.png"
        };

        // Act
        var result = await _userService.UpdateOwnProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("New Trainee Name", result.Data?.FullName);
        Assert.Equal("0987654321", result.Data?.PhoneNumber);
        Assert.Equal("Female", result.Data?.Gender);
        Assert.Equal(new DateOnly(1999, 12, 31), result.Data?.DateOfBirth);
        Assert.Equal("https://fitsocial.com/new.png", result.Data?.AvatarUrl);

        _userRepoMock.Verify(r => r.Update(traineeUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenCoach_UpdatesCoachProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Bio = "Old Bio",
            ExperienceYears = 3,
            ApprovalStatus = "APPROVED"
        };
        var coachUser = new User
        {
            UserId = userId,
            Email = "coach@fitsocial.com",
            PhoneNumber = "0901111222",
            FullName = "Coach Nguyen",
            RoleCode = RoleConstants.Coach,
            CoachProfileCoach = coachProfile,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync(It.IsAny<string>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Coach Nguyen Updated",
            PhoneNumber = "0909999888",
            Gender = "Male",
            Bio = "Updated Master Coach Bio",
            ExperienceYears = 7
        };

        // Act
        var result = await _userService.UpdateOwnProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        var coachResult = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.Equal("Coach Nguyen Updated", coachResult.FullName);
        Assert.Equal("Updated Master Coach Bio", coachResult.Bio);
        Assert.Equal(7, coachResult.ExperienceYears);
        Assert.Equal("APPROVED", coachResult.ApprovalStatus);

        _userRepoMock.Verify(r => r.Update(coachUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenFullNameHasNumbers_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Valid Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Name123"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateOwnProfileAsync(userId, request));
        Assert.Contains("numbers", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenPhoneFormatInvalid_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Valid Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Valid Name",
            PhoneNumber = "12345"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateOwnProfileAsync(userId, request));
        Assert.Contains("10-digit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenPhoneAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Valid Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);
        _userRepoMock.Setup(r => r.ExistsByPhoneAsync("0912345678", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Valid Name",
            PhoneNumber = "0912345678"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _userService.UpdateOwnProfileAsync(userId, request));
        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenDobInFuture_ThrowsValidationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Valid Name",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Valid Name",
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _userService.UpdateOwnProfileAsync(userId, request));
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Valid Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _userService.UpdateOwnProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Valid Name",
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "Valid Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => _userService.UpdateOwnProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenFieldsEmptyOrNull_RetainsExistingValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalUser = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "Original Trainee",
            PhoneNumber = "0901234567",
            Gender = "Male",
            DateOfBirth = new DateOnly(1995, 5, 20),
            AvatarUrl = "https://fitsocial.com/orig-avatar.jpg",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalUser);

        // All fields empty or null
        var request = new UpdateOwnProfileRequestDto
        {
            FullName = "",
            PhoneNumber = "   ",
            Gender = null,
            DateOfBirth = null,
            AvatarUrl = null
        };

        // Act
        var result = await _userService.UpdateOwnProfileAsync(userId, request);

        // Assert: all existing values must be retained
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Original Trainee", result.Data?.FullName);
        Assert.Equal("0901234567", result.Data?.PhoneNumber);
        Assert.Equal("Male", result.Data?.Gender);
        Assert.Equal(new DateOnly(1995, 5, 20), result.Data?.DateOfBirth);
        Assert.Equal("https://fitsocial.com/orig-avatar.jpg", result.Data?.AvatarUrl);

        _userRepoMock.Verify(r => r.Update(originalUser), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_WhenAvatarExplicitlyRemoved_ClearsAvatarUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userWithAvatar = new User
        {
            UserId = userId,
            RoleCode = RoleConstants.Trainee,
            FullName = "User Name",
            AvatarUrl = "https://fitsocial.com/current-avatar.jpg",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.GetUserForUpdateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWithAvatar);

        var request = new UpdateOwnProfileRequestDto
        {
            AvatarUrl = "[REMOVE]"
        };

        // Act
        var result = await _userService.UpdateOwnProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Null(result.Data?.AvatarUrl);
        Assert.Null(userWithAvatar.AvatarUrl);
    }
}
