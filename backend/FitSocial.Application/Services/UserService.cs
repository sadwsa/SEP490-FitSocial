using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork? _unitOfWork;

    public UserService(IUserRepository userRepository, IUnitOfWork? unitOfWork = null)
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUserRepository userRepository, IUnitOfWork unitOfWork = null!)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<UserProfileDto>> GetOwnProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindWithCoachProfileByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User profile could not be found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("This account has been locked. Please contact support.");
        }

        var role = user.RoleCode?.Trim().ToUpperInvariant() ?? RoleConstants.Trainee;

        if (role == RoleConstants.Coach)
        {
            var coachProfile = user.CoachProfileCoach;
            var coachDto = new CoachProfileDto
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Role = RoleConstants.Coach,
                CreatedAt = user.CreatedAt,
                Bio = coachProfile?.Bio,
                ExperienceYears = coachProfile?.ExperienceYears,
                ApprovalStatus = coachProfile?.ApprovalStatus ?? "PENDING"
            };

            return ApiResponseDto<UserProfileDto>.Ok(coachDto, "Profile retrieved successfully.");
        }

        var traineeDto = new TraineeProfileDto
        {
            FullName = user.FullName ?? string.Empty,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            Role = role == RoleConstants.Trainee ? RoleConstants.Trainee : role,
            CreatedAt = user.CreatedAt
        };

        return ApiResponseDto<UserProfileDto>.Ok(traineeDto, "Profile retrieved successfully.");
    }

    public async Task<ApiResponseDto<UserProfileResponseDto>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.FindUserProfileByIdAsync(userId, cancellationToken);
        if (user == null || user.IsLocked == true)
        {
            throw new NotFoundException("User profile not found.");
        }

        var role = user.RoleCode?.Trim().ToUpperInvariant() ?? RoleConstants.Trainee;

        if (role == RoleConstants.Coach)
        {
            var coachProfile = user.CoachProfileCoach;
            var reviewsWithRating = coachProfile?.Reviews?.Where(r => r.Rating != null).ToList() ?? new List<Review>();
            var totalReviews = reviewsWithRating.Count;
            var averageRating = totalReviews > 0
                ? reviewsWithRating.Select(r => (double)r.Rating!.Value).Average()
                : 0.0;

            var locationSummary = (coachProfile != null && coachProfile.Locations != null && coachProfile.Locations.Count > 0)
                ? string.Join(", ", coachProfile.Locations.Select(l => l.LocationName))
                : null;

            var coachDto = new CoachProfileResponseDto
            {
                UserId = user.UserId,
                FullName = user.FullName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Role = RoleConstants.Coach,
                Bio = coachProfile?.Bio,
                Location = locationSummary,
                CreatedAt = user.CreatedAt,
                ExperienceYears = coachProfile?.ExperienceYears,
                Rating = Math.Round(averageRating, 1),
                TotalReviews = totalReviews
            };

            return ApiResponseDto<UserProfileResponseDto>.Ok(coachDto, "User profile retrieved successfully.");
        }

        if (role == RoleConstants.Trainee)
        {
            var traineeDto = new TraineeProfileResponseDto
            {
                UserId = user.UserId,
                FullName = user.FullName ?? string.Empty,
                AvatarUrl = user.AvatarUrl,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Role = RoleConstants.Trainee,
                Bio = null,
                Location = null,
                CreatedAt = user.CreatedAt
            };

            return ApiResponseDto<UserProfileResponseDto>.Ok(traineeDto, "User profile retrieved successfully.");
        }

        throw new NotFoundException("User profile not found.");
    }

    public async Task<ApiResponseDto<StaffAdminOwnProfileDto>> GetStaffAdminProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Staff or Admin profile could not be found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("This account has been locked. Please contact support.");
        }

        var role = user.RoleCode?.Trim().ToUpperInvariant();
        if (!RoleConstants.IsAdminOrStaff(role))
        {
            throw new ForbiddenException("Access denied. Only Staff and Admin profiles are allowed.");
        }

        var profileDto = new StaffAdminOwnProfileDto
        {
            Avatar = user.AvatarUrl,
            FullName = user.FullName ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = role
        };

        return ApiResponseDto<StaffAdminOwnProfileDto>.Ok(profileDto, "Profile retrieved successfully.");
    }

    public async Task<ApiResponseDto<StaffAdminOwnProfileDto>> UpdateStaffAdminProfileAsync(
        Guid userId,
        UpdateStaffAdminProfileRequestDto request,
    public async Task<ApiResponseDto<UserProfileDto>> UpdateOwnProfileAsync(
        Guid userId,
        UpdateOwnProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ValidationException("Request cannot be null.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Staff or Admin profile could not be found.");
        var user = await _userRepository.GetUserForUpdateAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User profile could not be found.");
        }

        if (user.IsLocked == true)
        {
            throw new ForbiddenException("This account has been locked. Please contact support.");
        }

        var role = user.RoleCode?.Trim().ToUpperInvariant();
        if (!RoleConstants.IsAdminOrStaff(role))
        {
            throw new ForbiddenException("Access denied. Only Staff and Admin profiles are allowed.");
        }

        // Rule (UC_35.1):
        // Field không nhập / để trống -> giữ nguyên dữ liệu cũ trong database.
        // Field có nhập -> validate nghiêm ngặt; nếu hợp lệ mới cập nhật.

        // 1. FullName
        var role = user.RoleCode?.Trim().ToUpperInvariant() ?? RoleConstants.Trainee;
        if (role != RoleConstants.Trainee && role != RoleConstants.Coach)
        {
            throw new ForbiddenException("Only Trainee and Coach profiles can be updated through this endpoint.");
        }

        // Validate & Update FullName: only if provided and not empty
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            var trimmedFullName = request.FullName.Trim();
            if (trimmedFullName.Length < 2 || trimmedFullName.Length > 100)
            {
                throw new ValidationException("Full name must be between 2 and 100 characters.");
            }

            if (trimmedFullName.Any(char.IsDigit))
            {
                throw new ValidationException("Full name cannot contain numbers.");
            }

            user.FullName = trimmedFullName;
        }

        // 2. DateOfBirth
        // Validate & Update DateOfBirth: only if provided
        if (request.DateOfBirth.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (request.DateOfBirth.Value > today)
            {
                throw new ValidationException("Date of birth cannot be in the future.");
            }

            if (request.DateOfBirth.Value.Year < 1900)
            {
                throw new ValidationException("Date of birth is invalid.");
            }

            user.DateOfBirth = request.DateOfBirth.Value;
        }

        // 3. Gender
        // Validate & Update Gender: only if provided and not empty
        if (!string.IsNullOrWhiteSpace(request.Gender))
        {
            var g = request.Gender.Trim();
            if (string.Equals(g, "Male", StringComparison.OrdinalIgnoreCase))
            {
                user.Gender = "Male";
            }
            else if (string.Equals(g, "Female", StringComparison.OrdinalIgnoreCase))
            {
                user.Gender = "Female";
            }
            else if (string.Equals(g, "Other", StringComparison.OrdinalIgnoreCase))
            {
                user.Gender = "Other";
            }
            else
            {
                throw new ValidationException("Gender must be Male, Female, or Other.");
            }
        }

        // 4. PhoneNumber
        // Validate & Update PhoneNumber: only if provided and not empty
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var trimmedPhone = request.PhoneNumber.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedPhone, @"^0\d{9}$"))
            {
                throw new ValidationException("Phone number must be a valid 10-digit number starting with 0.");
            }

            if (await _userRepository.ExistsByPhoneAsync(trimmedPhone, userId, cancellationToken))
            {
                throw new ConflictException("Phone number is already registered by another account.");
            }

            user.PhoneNumber = trimmedPhone;
        }

        // 5. Avatar (accept Avatar or AvatarUrl)
        var avatarInput = !string.IsNullOrWhiteSpace(request.Avatar) ? request.Avatar : request.AvatarUrl;
        if (!string.IsNullOrWhiteSpace(avatarInput))
        {
            var trimmedAvatar = avatarInput.Trim();
        // Validate & Update AvatarUrl:
        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            var trimmedAvatar = request.AvatarUrl.Trim();
            if (trimmedAvatar.Equals("[REMOVE]", StringComparison.OrdinalIgnoreCase) ||
                trimmedAvatar.Equals("REMOVE", StringComparison.OrdinalIgnoreCase))
            {
                user.AvatarUrl = null;
            }
            else
            {
                if (trimmedAvatar.Length > 2048)
                {
                    throw new ValidationException("Avatar URL must not exceed 2048 characters.");
                }
                user.AvatarUrl = trimmedAvatar;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;

        // Coach-specific updates
        if (role == RoleConstants.Coach)
        {
            var coachProfile = user.CoachProfileCoach;
            if (coachProfile != null)
            {
                if (request.ExperienceYears.HasValue)
                {
                    if (request.ExperienceYears.Value < 0 || request.ExperienceYears.Value > 60)
                    {
                        throw new ValidationException("Years of experience must be between 0 and 60.");
                    }
                    coachProfile.ExperienceYears = request.ExperienceYears.Value;
                }

                if (!string.IsNullOrWhiteSpace(request.Bio))
                {
                    var trimmedBio = request.Bio.Trim();
                    if (trimmedBio.Length > 2000)
                    {
                        throw new ValidationException("Biography must not exceed 2000 characters.");
                    }
                    coachProfile.Bio = trimmedBio;
                }

                coachProfile.UpdatedAt = DateTime.UtcNow;
            }
        }

        _userRepository.Update(user);
        if (_unitOfWork != null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var profileDto = new StaffAdminOwnProfileDto
        {
            Avatar = user.AvatarUrl,
            FullName = user.FullName ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email,
            Role = role
        };

        return ApiResponseDto<StaffAdminOwnProfileDto>.Ok(profileDto, "Profile updated successfully.");
        // Return updated UserProfileDto
        if (role == RoleConstants.Coach)
        {
            var coachProfile = user.CoachProfileCoach;
            var coachDto = new CoachProfileDto
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                Role = RoleConstants.Coach,
                CreatedAt = user.CreatedAt,
                Bio = coachProfile?.Bio,
                ExperienceYears = coachProfile?.ExperienceYears,
                ApprovalStatus = coachProfile?.ApprovalStatus ?? "PENDING"
            };

            return ApiResponseDto<UserProfileDto>.Ok(coachDto, "Profile updated successfully.");
        }

        var traineeDto = new TraineeProfileDto
        {
            FullName = user.FullName ?? string.Empty,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.PhoneNumber,
            DateOfBirth = user.DateOfBirth,
            Gender = user.Gender,
            Role = RoleConstants.Trainee,
            CreatedAt = user.CreatedAt
        };

        return ApiResponseDto<UserProfileDto>.Ok(traineeDto, "Profile updated successfully.");
    }
}
