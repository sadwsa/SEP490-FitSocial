using System;
using System.Linq;
using System.Security.Claims;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FitSocial.Infrastructure.Services;

public class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user == null) return null;
            var claimVal = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user.FindFirst("sub")?.Value
                        ?? user.FindFirst("userId")?.Value;
            return Guid.TryParse(claimVal, out var id) ? id : null;
        }
    }

    public string? Role =>
        _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("role")?.Value;

    public string? IpAddress
    {
        get
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext == null) return null;
            var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            return !string.IsNullOrWhiteSpace(forwarded)
                ? forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
                : httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
