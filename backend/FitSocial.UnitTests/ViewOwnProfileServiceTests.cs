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

    /// <summary>
    /// UTCID01: Normal case - Retrieves personal coach profile with both valid and expired certificates, returning only valid certificates.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = Coach has both valid certificates (APPROVED, unexpired) and expired certificates (APPROVED, expired), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = N, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID02: Normal case - Retrieves personal profile for trainee user successfully.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "TRAINEE", CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return TraineeProfileDto with Success=true.
    /// Result: Type = N, Status = Passed
    /// </summary>
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
            FullName = "John Doe",
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
        Assert.Equal("John Doe", traineeDto.FullName);
        Assert.Equal("trainee.jane@fitsocial.com", traineeDto.Email);
        Assert.Equal("0987654321", traineeDto.PhoneNumber);
        Assert.Equal("https://fitsocial.com/avatars/trainee.jpg", traineeDto.AvatarUrl);
        Assert.Equal(new DateOnly(1998, 8, 20), traineeDto.DateOfBirth);
        Assert.Equal("Female", traineeDto.Gender);
        Assert.Equal(RoleConstants.Trainee, traineeDto.Role);

        _userRepoMock.Verify(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// UTCID03: Normal case - Coach user has only valid certificates.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = Coach has only valid certificates (APPROVED, unexpired), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = N, Status = Passed
    /// </summary>
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
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID04: Abnormal case - User does not exist in repository.
    /// Conditions: Precondition = Database connected, Target User = empty/null, IsLocked = false, RoleCode = empty/null, CoachProfile = empty/null, FullName = empty/null, Repository = Normal execution.
    /// Confirm: NotFoundException ("User profile could not be found.").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID05: Abnormal case - User account is locked.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = true, RoleCode = "TRAINEE", CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: ForbiddenException ("Account locked").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID06: Abnormal case - Repository throws database exception.
    /// Conditions: Precondition = Database connection failed, Target User = empty/null, IsLocked = false, RoleCode = empty/null, CoachProfile = empty/null, FullName = empty/null, Repository = Repository throws DB exception.
    /// Confirm: InvalidOperationException ("DB connection error").
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID07: Abnormal case - CancellationToken is cancelled before query completes.
    /// Conditions: Precondition = Database connected, Target User = empty/null, IsLocked = false, RoleCode = empty/null, CoachProfile = empty/null, FullName = empty/null, Repository = CancellationToken requested cancel.
    /// Confirm: OperationCanceledException.
    /// Result: Type = A, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID08: Boundary case - Coach user with null CoachProfileCoach.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = CoachProfileCoach = null (Coach profile has not been created), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task GetOwnProfileAsync_WhenCoachProfileIsNull_ReturnsCoachDtoWithDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            Email = "coach.noprofile@fitsocial.com",
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID09: Boundary case - Coach certificates list is empty.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = Coach has no certificates (Empty list, Count = 0), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
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
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID10: Boundary case - Coach certificates are all expired.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = Coach has only expired certificates (APPROVED, expired), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
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
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID11: Boundary case - Coach certificates are in PENDING status.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "COACH", CoachProfile = Coach has only pending certificates (PENDING, unexpired), FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
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
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID12: Boundary case - User FullName is null in database.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "TRAINEE", CoachProfile = empty/null, FullName = empty/null, Repository = Normal execution.
    /// Confirm: Return TraineeProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
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

    /// <summary>
    /// UTCID13: Boundary case - RoleCode is null in database, defaults to Trainee role.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = empty/null, CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return TraineeProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task GetOwnProfileAsync_WhenRoleCodeIsNull_DefaultsToTraineeRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userWithNullRole = new User
        {
            UserId = userId,
            Email = "nullrole@fitsocial.com",
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID14: Boundary case - User has custom/staff role, returns TraineeDto with specified role.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "STAFF", CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return TraineeProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task GetOwnProfileAsync_WhenUserHasCustomRole_ReturnsTraineeDtoWithSpecifiedRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var memberUser = new User
        {
            UserId = userId,
            Email = "staffrole@fitsocial.com",
            FullName = "John Doe",
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

    /// <summary>
    /// UTCID15: Boundary case - User account is active with IsLocked = false.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "TRAINEE", CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return TraineeProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task GetOwnProfileAsync_WhenIsLockedIsFalse_ReturnsProfileSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeUser = new User
        {
            UserId = userId,
            Email = "activeuser@fitsocial.com",
            FullName = "John Doe",
            RoleCode = RoleConstants.Trainee,
            IsLocked = false
        };

        _userRepoMock.Setup(r => r.FindWithCoachProfileByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeUser);

        // Act
        var result = await _userService.GetOwnProfileAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    /// <summary>
    /// UTCID16: Boundary case - RoleCode has whitespace or lowercase, normalizes role to COACH.
    /// Conditions: Precondition = Database connected, Target User = Found, IsLocked = false, RoleCode = "  coach  ", CoachProfile = empty/null, FullName = "John Doe", Repository = Normal execution.
    /// Confirm: Return CoachProfileDto with Success=true.
    /// Result: Type = B, Status = Passed
    /// </summary>
    [Fact]
    public async Task GetOwnProfileAsync_WhenRoleCodeHasWhitespaceOrLowerCase_NormalizesRoleCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var coachUser = new User
        {
            UserId = userId,
            Email = "whitespace@fitsocial.com",
            FullName = "John Doe",
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

