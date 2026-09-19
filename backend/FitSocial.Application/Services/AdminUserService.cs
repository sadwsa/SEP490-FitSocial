using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminUserService(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
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

            var dtos = users.Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                FullName = u.FullName ?? u.Email.Split('@')[0],
                RoleCode = u.RoleCode ?? "TRAINEE",
                IsLocked = u.IsLocked ?? false,
                CreatedAt = u.CreatedAt,
                LastActiveAt = u.LastActiveAt
            }).ToList();

            var pagedResult = PagedResultDto<AdminUserDto>.Create(dtos, totalCount, pageNumber, pageSize);

            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Ok(pagedResult, "User list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<AdminUserDto>>.Fail($"Error retrieving users: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> SetUserLockStatusAsync(Guid userId, bool isLocked, Guid? adminId = null, CancellationToken cancellationToken = default)
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
}
