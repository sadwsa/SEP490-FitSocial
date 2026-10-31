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

public class ViewOwnProfileServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UserService _userService;

    public ViewOwnProfileServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userService = new UserService(_userRepoMock.Object, _unitOfWorkMock.Object);
    }

    #region Normal Cases (N)

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachWithBothValidAndExpiredCertificates_ReturnsCoachProfileDtoWithValidCertificatesOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "CrossFit Level 2",
            CertificateUrl = "https://fitsocial.com/certs/cf2.pdf",
            IssuedBy = "CrossFit LLC",
            IssuedDate = today.AddYears(-1),
            ExpiryDate = today.AddYears(1),
            VerificationStatus = "APPROVED"
        };
        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Basic Fitness",
            CertificateUrl = "https://fitsocial.com/certs/basic.pdf",
            IssuedBy = "Fitness Academy",
            IssuedDate = today.AddYears(-3),
            ExpiryDate = today.AddDays(-10),
            VerificationStatus = "APPROVED"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Bio = "Professional fitness instructor with 5 years experience.",
            ExperienceYears = 5,
            ApprovalStatus = "APPROVED",
            CoachCertificates = new List<CoachCertificate> { validCert, expiredCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.john@fitsocial.com",
            PhoneNumber = "0912345678",
            FullName = "John Doe",
            RoleCode = RoleConstants.Coach,
            AvatarUrl = "https://fitsocial.com/avatars/coach.jpg",
            DateOfBirth = new DateOnly(1992, 4, 12),
            Gender = "Male",
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Profile retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);

        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.Equal("John Doe", coachDto.FullName);
        Assert.Equal("coach.john@fitsocial.com", coachDto.Email);
        Assert.Equal("0912345678", coachDto.PhoneNumber);
        Assert.Equal("https://fitsocial.com/avatars/coach.jpg", coachDto.AvatarUrl);
        Assert.Equal(new DateOnly(1992, 4, 12), coachDto.DateOfBirth);
        Assert.Equal("Male", coachDto.Gender);
        Assert.Equal(RoleConstants.Coach, coachDto.Role);
        Assert.Equal("Professional fitness instructor with 5 years experience.", coachDto.Bio);
        Assert.Equal(5, coachDto.ExperienceYears);
        Assert.Equal("APPROVED", coachDto.ApprovalStatus);
        Assert.True(coachDto.HasExpiredCertificates);
        Assert.True(coachDto.HasValidCertificates);
        Assert.Single(coachDto.Certificates);

        var certDto = coachDto.Certificates[0];
        Assert.Equal(validCert.CertificateId, certDto.CertificateId);
        Assert.Equal(validCert.CertificateName, certDto.CertificateName);
        Assert.Equal(validCert.CertificateUrl, certDto.CertificateUrl);
        Assert.Equal(validCert.IssuedBy, certDto.IssuedBy);
        Assert.Equal(validCert.IssuedDate, certDto.IssuedDate);
        Assert.Equal(validCert.ExpiryDate, certDto.ExpiryDate);
        Assert.Equal("APPROVED", certDto.VerificationStatus);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenTrainee_ReturnsTraineeProfileDtoSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            Email = "trainee.jane@fitsocial.com",
            PhoneNumber = "0987654321",
            FullName = "Jane Smith",
            RoleCode = RoleConstants.Trainee,
            AvatarUrl = "https://fitsocial.com/avatars/trainee.jpg",
            DateOfBirth = new DateOnly(1998, 8, 20),
            Gender = "Female",
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Profile retrieved successfully.", result.Message);
        Assert.NotNull(result.Data);

        var traineeDto = Assert.IsType<TraineeProfileDto>(result.Data);
        Assert.Equal("Jane Smith", traineeDto.FullName);
        Assert.Equal("trainee.jane@fitsocial.com", traineeDto.Email);
        Assert.Equal("0987654321", traineeDto.PhoneNumber);
        Assert.Equal("https://fitsocial.com/avatars/trainee.jpg", traineeDto.AvatarUrl);
        Assert.Equal(new DateOnly(1998, 8, 20), traineeDto.DateOfBirth);
        Assert.Equal("Female", traineeDto.Gender);
        Assert.Equal(RoleConstants.Trainee, traineeDto.Role);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachWithOnlyValidCertificates_ReturnsValidCertificatesAndHasExpiredFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var validCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "NASM-CPT",
            CertificateUrl = "https://fitsocial.com/certs/nasm.pdf",
            IssuedBy = "NASM",
            IssuedDate = today.AddYears(-1),
            ExpiryDate = today.AddYears(2),
            VerificationStatus = "APPROVED"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Bio = "Certified Trainer",
            ExperienceYears = 3,
            ApprovalStatus = "APPROVED",
            CoachCertificates = new List<CoachCertificate> { validCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.valid@fitsocial.com",
            FullName = "Valid Coach",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.True(coachDto.HasValidCertificates);
        Assert.Single(coachDto.Certificates);
    }

    #endregion

    #region Abnormal Cases (A)

    [Fact]
    public async Task GetOwnProfileAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetOwnProfileAsync(userId));
        Assert.Equal("User profile could not be found.", ex.Message);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenUserIsLocked_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lockedUser = new User
        {
            UserId = userId,
            Email = "locked@fitsocial.com",
            FullName = "Locked User",
            RoleCode = RoleConstants.Trainee,
            IsLocked = true
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => _userService.GetOwnProfileAsync(userId));
        Assert.Equal("This account has been locked. Please contact support.", ex.Message);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failure"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _userService.GetOwnProfileAsync(userId));
        Assert.Equal("Database connection failure", ex.Message);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenCancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, cts.Token))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _userService.GetOwnProfileAsync(userId, cts.Token));

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, cts.Token), Times.Once);
    }

    #endregion

    #region Boundary Cases (B)

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachProfileIsNull_ReturnsCoachDtoWithDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.noprofile@fitsocial.com",
            FullName = "New Coach",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = null
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.Null(coachDto.Bio);
        Assert.Null(coachDto.ExperienceYears);
        Assert.Equal("PENDING", coachDto.ApprovalStatus);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachCertificatesListIsEmpty_ReturnsEmptyCertificates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            Bio = "Coach without certs",
            ExperienceYears = 1,
            ApprovalStatus = "APPROVED",
            CoachCertificates = new List<CoachCertificate>()
        };

        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.nocerts@fitsocial.com",
            FullName = "No Certs Coach",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachCertificatesAreAllExpired_ReturnsHasExpiredTrueAndEmptyCertificates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var expiredCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Expired Cert",
            CertificateUrl = "https://fitsocial.com/certs/exp.pdf",
            IssuedBy = "Old Org",
            IssuedDate = today.AddYears(-3),
            ExpiryDate = today.AddDays(-1),
            VerificationStatus = "APPROVED"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            ApprovalStatus = "APPROVED",
            CoachCertificates = new List<CoachCertificate> { expiredCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.expired@fitsocial.com",
            FullName = "Expired Cert Coach",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.True(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachCertificatesNotApproved_ReturnsHasValidFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var pendingCert = new CoachCertificate
        {
            CertificateId = Guid.NewGuid(),
            CertificateName = "Pending Cert",
            CertificateUrl = "https://fitsocial.com/certs/pending.pdf",
            IssuedBy = "Academy",
            IssuedDate = today.AddMonths(-1),
            ExpiryDate = today.AddYears(1),
            VerificationStatus = "PENDING"
        };

        var coachProfile = new CoachProfile
        {
            CoachId = userId,
            ApprovalStatus = "APPROVED",
            CoachCertificates = new List<CoachCertificate> { pendingCert }
        };

        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.pendingcert@fitsocial.com",
            FullName = "Pending Cert Coach",
            RoleCode = RoleConstants.Coach,
            IsLocked = false,
            CoachProfileCoach = coachProfile
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.False(coachDto.HasExpiredCertificates);
        Assert.False(coachDto.HasValidCertificates);
        Assert.Empty(coachDto.Certificates);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenUserFullNameIsNull_ReturnsEmptyStringForFullName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var traineeUser = new User
        {
            UserId = userId,
            Email = "nullname@fitsocial.com",
            FullName = null,
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(traineeUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Data?.FullName);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenRoleCodeIsNull_DefaultsToTraineeRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userWithNullRole = new User
        {
            UserId = userId,
            Email = "nullrole@fitsocial.com",
            FullName = "Default Trainee",
            RoleCode = null,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWithNullRole);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var traineeDto = Assert.IsType<TraineeProfileDto>(result.Data);
        Assert.Equal(RoleConstants.Trainee, traineeDto.Role);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenUserHasCustomRole_ReturnsTraineeDtoWithSpecifiedRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var memberUser = new User
        {
            UserId = userId,
            Email = "staffrole@fitsocial.com",
            FullName = "Staff Or Member",
            RoleCode = "STAFF",
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var traineeDto = Assert.IsType<TraineeProfileDto>(result.Data);
        Assert.Equal("STAFF", traineeDto.Role);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenIsLockedIsNull_ReturnsProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userWithNullLocked = new User
        {
            UserId = userId,
            Email = "nulllocked@fitsocial.com",
            FullName = "Null Locked User",
            RoleCode = RoleConstants.Trainee,
            IsLocked = null
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWithNullLocked);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetOwnProfileAsync_WhenRoleCodeHasWhitespaceOrLowerCase_NormalizesRoleCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            Email = "whitespace@fitsocial.com",
            FullName = "Normalized Role Coach",
            RoleCode = "  coach  ",
            IsLocked = false,
            CoachProfileCoach = new CoachProfile
            {
                CoachId = userId,
                ApprovalStatus = "APPROVED"
            }
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coachUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        var coachDto = Assert.IsType<CoachProfileDto>(result.Data);
        Assert.Equal(RoleConstants.Coach, coachDto.Role);
    }

    #endregion
}

