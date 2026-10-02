using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Domain.Entities;

namespace FitSocial.Application.Interfaces;

public interface ITokenService
{
    Task<AuthResponseDto> CreateSessionAsync(
        User user, string? deviceInfo = null, bool rememberMe = true, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AuthResponseDto>> RefreshSessionAsync(
        string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
