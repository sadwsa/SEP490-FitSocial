using System.Security.Cryptography;
using System.Text;
using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class TokenService : ITokenService
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> CreateSessionAsync(
        User user, string? deviceInfo = null, CancellationToken cancellationToken = default)
    {
        var (accessToken, expiresAt) = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = GenerateOpaqueToken();

        var refreshDaysStr = _configuration["Jwt:RefreshExpiryDays"];
        if (!int.TryParse(refreshDaysStr, out var refreshDays) || refreshDays <= 0)
        {
            refreshDays = 30;
        }

        await _refreshTokens.AddAsync(new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            DeviceInfo = string.IsNullOrWhiteSpace(deviceInfo) ? null : deviceInfo.Trim(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        // NOTE: caller owns the final SaveChangesAsync (same transaction as user changes).
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                RoleCode = user.RoleCode,
                AvatarUrl = user.AvatarUrl
            }
        };
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RefreshSessionAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Missing refresh token.");
        }

        var stored = await _refreshTokens.FindByHashAsync(HashToken(refreshToken.Trim()), cancellationToken);
        if (stored == null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid or expired refresh token. Please sign in again.");
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user == null || user.IsLocked == true)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Account is not available.");
        }

        // Rotation: revoke the used token, issue a brand-new session
        stored.RevokedAt = DateTime.UtcNow;
        var response = await CreateSessionAsync(user, stored.DeviceInfo, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not refresh the session. Please sign in again.");
        }

        return ApiResponseDto<AuthResponseDto>.Ok(response, "Session refreshed.");
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        try
        {
            var stored = await _refreshTokens.FindByHashAsync(HashToken(refreshToken.Trim()), cancellationToken);
            if (stored != null && stored.RevokedAt == null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Logout must never fail because of bookkeeping, but the failure
            // is logged: a revoked-missed token stays valid until it expires.
            _logger.LogWarning(ex, "Failed to revoke refresh token on logout.");
        }
    }

    private static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
