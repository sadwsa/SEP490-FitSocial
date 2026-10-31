using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Moq;
using Xunit;

namespace FitSocial.UnitTests;

public class ViewOtherUserProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UserService _userService;

    public ViewOtherUserProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userService = new UserService(_userRepoMock.Object, _unitOfWorkMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task GetUserProfileAsync_WhenCoachExistsWithFullDetails_ReturnsCoachProfileResponseDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "CrossFit Level 1",
            CertificateUrl = "https://fitsocial.com/certs/cf1.pdf",
            IssuedBy = "CrossFit LLC",
            IssuedDate = today.AddYears(-1),
            ExpiryDate = today.AddYears(1),
            VerificationStatus = "APPROVED"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Bio = "Certified fitness coach with 5 years experience.",
            ExperienceYears = 5,
            Locations = new List<Location>
            {
                new() { LocationName = "District 1, HCMC" },
                new() { LocationName = "District 3, HCMC" }
            },
            Reviews = new List<Review>
            {
                new() { Rating = 4 },
                new() { Rating = 5 }
            },
            CoachCertificates = new List<CoachCertificate> { validCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            AvatarUrl = "https://fitsocial.com/avatars/coach.jpg",
            DateOfBirth = new DateOnly(1990, 5, 15),
            Gender = "Male",
            RoleCode = RoleConstants.Coach,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("User profile retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);

        var coachDto = Assert.IsType<CoachProfileResponseDto>(result.Data);
        Assert.Equal(userId, coachDto.UserId);
        Assert.Equal("John Doe", coachDto.FullName);
        Assert.Equal("https://fitsocial.com/avatars/coach.jpg", coachDto.AvatarUrl);
        Assert.Equal(new DateOnly(1990, 5, 15), coachDto.DateOfBirth);
        Assert.Equal("Male", coachDto.Gender);
        Assert.Equal(RoleConstants.Coach, coachDto.Role);
        Assert.Equal("Certified fitness coach with 5 years experience.", coachDto.Bio);
        Assert.Equal("District 1, HCMC, District 3, HCMC", coachDto.Location);
        Assert.Equal(5, coachDto.ExperienceYears);
        Assert.Equal(4.5, coachDto.Rating);
        Assert.Equal(2, coachDto.TotalReviews);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.True(coachDto.HasValidCertificates);
        Assert.Single(coachDto.Certificates);
        Assert.Equal("CrossFit Level 1", coachDto.Certificates[0].CertificateName);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenTraineeExists_ReturnsTraineeProfileResponseDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            AvatarUrl = "https://fitsocial.com/avatars/trainee.jpg",
            DateOfBirth = new DateOnly(1998, 8, 20),
            Gender = "Female",
            RoleCode = RoleConstants.Trainee,
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("User profile retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);

        var traineeDto = Assert.IsType<TraineeProfileResponseDto>(result.Data);
        Assert.Equal(userId, traineeDto.UserId);
        Assert.Equal("John Doe", traineeDto.FullName);
        Assert.Equal("https://fitsocial.com/avatars/trainee.jpg", traineeDto.AvatarUrl);
        Assert.Equal(new DateOnly(1998, 8, 20), traineeDto.DateOfBirth);
        Assert.Equal("Female", traineeDto.Gender);
        Assert.Equal(RoleConstants.Trainee, traineeDto.Role);
        Assert.Null(traineeDto.Bio);
        Assert.Null(traineeDto.Location);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenCoachHasExpiredAndValidCertificates_ReturnsOnlyValidCertificatesAndFlagsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Active Certified Trainer",
            CertificateUrl = "https://fitsocial.com/certs/valid.pdf",
            IssuedBy = "NASM",
            IssuedDate = today.AddYears(-1),
            ExpiryDate = today.AddYears(1),
            VerificationStatus = "APPROVED"
        };

        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Old Fitness Cert",
            CertificateUrl = "https://fitsocial.com/certs/expired.pdf",
            IssuedBy = "Local Gym",
            IssuedDate = today.AddYears(-3),
            ExpiryDate = today.AddDays(-10),
            VerificationStatus = "APPROVED"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            CoachCertificates = new List<CoachCertificate> { validCert, expiredCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileResponseDto>(result.Data);
        Assert.True(coachDto.HasExpiredCertificates);
        Assert.True(coachDto.HasValidCertificates);
        Assert.Single(coachDto.Certificates);
        Assert.Equal(validCert.CertificateId, coachDto.Certificates[0].CertificateId);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task GetUserProfileAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetUserProfileAsync(userId));
        Assert.Equal("User profile not found.", ex.Message);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUserIsLocked_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            RoleCode = RoleConstants.Trainee,
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetUserProfileAsync(userId));
        Assert.Equal("User profile not found.", ex.Message);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenUserRoleIsAdmin_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            RoleCode = RoleConstants.Admin,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetUserProfileAsync(userId));
        Assert.Equal("User profile not found.", ex.Message);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failure"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _userService.GetUserProfileAsync(userId));
        Assert.Equal("Database connection failure", ex.Message);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _userService.GetUserProfileAsync(userId, cts.Token));

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, cts.Token), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task GetUserProfileAsync_WhenCoachProfileIsNull_ReturnsCoachDtoWithDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = null
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileResponseDto>(result.Data);
        Assert.Equal(0.0, coachDto.Rating);
        Assert.Equal(0, coachDto.TotalReviews);
        Assert.Null(coachDto.Location);
        Assert.Null(coachDto.Bio);
        Assert.Null(coachDto.ExperienceYears);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenCoachReviewsHaveNullRatingAndLocationsEmpty_ReturnsZeroRatingAndNullLocation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Locations = new List<Location>(),
            Reviews = new List<Review>
            {
                new() { Rating = null }
            },
            CoachCertificates = new List<CoachCertificate>()
        };

        var coachUser = new User
        {
            UserId = userId,
            FullName = "John Doe",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileResponseDto>(result.Data);
        Assert.Equal(0.0, coachDto.Rating);
        Assert.Equal(0, coachDto.TotalReviews);
        Assert.Null(coachDto.Location);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserProfileAsync_WhenFullNameAndRoleCodeAreNull_DefaultsToTraineeAndEmptyFullName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userWithNulls = new User
        {
            UserId = userId,
            FullName = null,
            RoleCode = null,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWithNulls);

        // Act
        var result = await _userService.GetUserProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var traineeDto = Assert.IsType<TraineeProfileResponseDto>(result.Data);
        Assert.Equal(string.Empty, traineeDto.FullName);
        Assert.Equal(RoleConstants.Trainee, traineeDto.Role);

        _userRepoMock.Verify(r => r.FindUserProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

