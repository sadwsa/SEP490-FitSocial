using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FitSocial.Client.Services.Auth;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _localStorage;
    private readonly IServiceProvider _serviceProvider;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public CustomAuthenticationStateProvider(ITokenStorage localStorage, IServiceProvider serviceProvider)
    {
        _localStorage = localStorage;
        _serviceProvider = serviceProvider;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(Anonymous);
            }

            if (IsTokenExpired(token, TimeSpan.FromSeconds(60)))
            {
                var authService = _serviceProvider.GetRequiredService<IAuthService>();
                var refreshed = await authService.RefreshTokenAsync();
                if (refreshed)
                {
                    token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
                }
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new AuthenticationState(Anonymous);
                }
            }

            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    internal static bool IsTokenExpired(string jwt, TimeSpan skew)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return true;
            var payload = parts[1];
            var bytes = DecodeBase64Url(payload);
            using var doc = JsonDocument.Parse(bytes);
            if (!doc.RootElement.TryGetProperty("exp", out var expEl)) return false;
            var expSeconds = expEl.GetInt64();
            var exp = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            return exp - TimeSpan.FromMinutes(0) <= DateTime.UtcNow + skew;
        }
        catch
        {
            return true;
        }
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var authenticatedUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
        var authState = Task.FromResult(new AuthenticationState(authenticatedUser));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        var authState = Task.FromResult(new AuthenticationState(Anonymous));
        NotifyAuthenticationStateChanged(authState);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();
        var parts = jwt.Split('.');
        if (parts.Length < 2) return claims;

        var payload = parts[1];
        var jsonBytes = DecodeBase64Url(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs == null) return claims;

        foreach (var kvp in keyValuePairs)
        {
            var key = kvp.Key;

            if (key.Equals("role", StringComparison.OrdinalIgnoreCase) || 
                key.Equals("roles", StringComparison.OrdinalIgnoreCase) ||
                key.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            {
                key = ClaimTypes.Role;
            }
            else if (key.Equals("name", StringComparison.OrdinalIgnoreCase) || 
                     key.Equals("unique_name", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals(ClaimTypes.Name, StringComparison.OrdinalIgnoreCase))
            {
                key = ClaimTypes.Name;
            }
            else if (key.Equals("email", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals(ClaimTypes.Email, StringComparison.OrdinalIgnoreCase))
            {
                key = ClaimTypes.Email;
            }
            else if (key.Equals("sub", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals("nameid", StringComparison.OrdinalIgnoreCase) ||
                     key.Equals(ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                key = ClaimTypes.NameIdentifier;
            }

            if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    claims.Add(new Claim(key, item.GetString() ?? item.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(key, kvp.Value?.ToString() ?? string.Empty));
            }
        }

        return claims;
    }

    private static byte[] DecodeBase64Url(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
