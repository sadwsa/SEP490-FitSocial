using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;

namespace FitSocial.Application.Interfaces;

public interface IUserService
{
    Task<ApiResponseDto<UserProfileDto>> GetOwnProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<UserProfileResponseDto>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<StaffAdminOwnProfileDto>> GetStaffAdminProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<UserProfileDto>> UpdateOwnProfileAsync(Guid userId, UpdateOwnProfileRequestDto request, CancellationToken cancellationToken = default);
}
