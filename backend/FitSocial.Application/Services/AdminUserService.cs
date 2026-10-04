using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public AdminUserService(IUserRepository userRepository, IUnitOfWork unitOfWork, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<ApiResponseDto<PagedResultDto<AdminUserDto>>> GetUsersAsync(
        string? search = null,
        string? role = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (users, totalCount) = await _userRepository.ListUsersForAdminAsync(
                search, role, isLocked, pageNumber, pageSize, cancellationToken);

            var dtos = users.Select(u => MapToAdminDto(u)).ToList();

            var pagedResult = PagedResultDto<AdminUserDto>.Create(dtos, totalCount, pageNumber, pageSize);

            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Ok(pagedResult, "User list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Fail($"Error retrieving users: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<PagedResultDto<AdminUserDto>>> GetStaffUsersAsync(
        string? search = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (users, totalCount) = await _userRepository.ListStaffForAdminAsync(
                search, isLocked, pageNumber, pageSize, cancellationToken);

            var dtos = users.Select(u => MapToAdminDto(u)).ToList();

            var pagedResult = PagedResultDto<AdminUserDto>.Create(dtos, totalCount, pageNumber, pageSize);

            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Ok(pagedResult, "Staff account list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Fail($"Error retrieving staff accounts: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> SetUserLockStatusAsync(
        Guid userId,
        bool isLocked,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return ApiResponseDto<bool>.Fail("User not found.");
            }

            user.IsLocked = isLocked;
            user.LockedBy = isLocked ? adminId : null;
            if (isLocked)
            {
                // Invalidate all active tokens for locked user
                user.TokenVersion += 1;
            }
            user.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var actionText = isLocked ? "locked" : "unlocked";
            return ApiResponseDto<bool>.Ok(true, $"User '{user.FullName ?? user.Email}' has been {actionText}.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"Error setting lock status: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<AdminUserDto>> WarnUserAsync(
        Guid userId,
        string? reason = null,
        Guid? staffId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                return ApiResponseDto<AdminUserDto>.Fail("User not found.");
            }

            user.WarningCount += 1;
            user.UpdatedAt = DateTime.UtcNow;

            // When reaching 3 warnings, automatically lock the account and invalidate tokens
            if (user.WarningCount >= 3)
            {
                user.IsLocked = true;
                user.LockedBy = staffId;
                user.TokenVersion += 1;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var msg = user.WarningCount >= 3
                ? $"User '{user.FullName ?? user.Email}' has reached {user.WarningCount} warnings and has been locked."
                : $"Warning issued to '{user.FullName ?? user.Email}'. Total warnings: {user.WarningCount}.";

            return ApiResponseDto<AdminUserDto>.Ok(MapToAdminDto(user), msg);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<AdminUserDto>.Fail($"Error issuing warning: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<AdminUserDto>> CreateStaffAccountAsync(
        CreateStaffAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request == null)
            {
                return ApiResponseDto<AdminUserDto>.Fail("Request cannot be null.");
            }

            var trimmedFullName = request.FullName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedFullName) || trimmedFullName.Length < 2)
            {
                return ApiResponseDto<AdminUserDto>.Fail("Full name must be at least 2 characters.");
            }

            if (trimmedFullName.Any(char.IsDigit))
            {
                return ApiResponseDto<AdminUserDto>.Fail("Full name cannot contain numbers.");
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return ApiResponseDto<AdminUserDto>.Fail("Email is required.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            if (await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
            {
                return ApiResponseDto<AdminUserDto>.Fail("Email is already registered.");
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return ApiResponseDto<AdminUserDto>.Fail("Password must be at least 6 characters.");
            }

            if (request.Password != request.ConfirmPassword)
            {
                return ApiResponseDto<AdminUserDto>.Fail("Passwords do not match.");
            }

            var trimmedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedPhone))
            {
                if (trimmedPhone.Any(char.IsLetter))
                {
                    return ApiResponseDto<AdminUserDto>.Fail("Phone number cannot contain letters.");
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedPhone, @"^0\d{9}$"))
                {
                    return ApiResponseDto<AdminUserDto>.Fail("Phone number must be a valid 10-digit number starting with 0.");
                }

                if (await _userRepository.ExistsByPhoneAsync(trimmedPhone, cancellationToken: cancellationToken))
                {
                    return ApiResponseDto<AdminUserDto>.Fail("Phone number is already registered.");
                }
            }

            var now = DateTime.UtcNow;
            var staffUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = trimmedFullName,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                PhoneNumber = trimmedPhone,
                RoleCode = RoleConstants.Staff,
                IsInternal = true,
                IsLocked = false,
                TokenVersion = 1,
                WarningCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _userRepository.AddAsync(staffUser, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponseDto<AdminUserDto>.Ok(MapToAdminDto(staffUser), "Staff account created successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<AdminUserDto>.Fail($"Error creating staff account: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> CheckEmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return ApiResponseDto<bool>.Fail("Email cannot be empty.");
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            var exists = await _userRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken);
            return ApiResponseDto<bool>.Ok(exists, exists ? "Email is already registered." : "Email is available.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"Error checking email availability: {ex.Message}");
        }
    }

    private static AdminUserDto MapToAdminDto(User u)
    {
        return new AdminUserDto
        {
            UserId = u.UserId,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            FullName = u.FullName ?? u.Email.Split('@')[0],
            RoleCode = u.RoleCode ?? "TRAINEE",
            IsLocked = u.IsLocked ?? false,
            WarningCount = u.WarningCount,
            TokenVersion = u.TokenVersion,
            CreatedAt = u.CreatedAt,
            LastActiveAt = u.LastActiveAt
        };
    }
}
