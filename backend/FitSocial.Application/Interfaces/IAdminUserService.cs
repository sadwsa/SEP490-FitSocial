using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;

namespace FitSocial.Application.Interfaces;

public interface IAdminUserService
{
    Task<ApiResponseDto<List<AdminUserDto>>> GetUsersAsync(string? search = null, string? role = null, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> SetUserLockStatusAsync(Guid userId, bool isLocked, Guid? adminId = null, CancellationToken cancellationToken = default);
}
