using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;

namespace FitSocial.Application.Interfaces;

public interface IAdminUserService
{
    Task<ApiResponseDto<PagedResultDto<AdminUserDto>>> GetUsersAsync(
        string? search = null,
        string? role = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<PagedResultDto<AdminUserDto>>> GetStaffUsersAsync(
        string? search = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> SetUserLockStatusAsync(Guid userId, bool isLocked, Guid? adminId = null, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AdminUserDto>> WarnUserAsync(Guid userId, string? reason = null, Guid? staffId = null, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AdminUserDto>> CreateStaffAccountAsync(CreateStaffAccountRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> CheckEmailExistsAsync(string email, CancellationToken cancellationToken = default);
}
