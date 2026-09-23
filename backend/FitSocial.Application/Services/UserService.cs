using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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
}
