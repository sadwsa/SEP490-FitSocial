using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace FitSocial.Client.Services.Auth;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _localStorage;
    private readonly HttpClient _http;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public CustomAuthenticationStateProvider(ITokenStorage localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
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

            // Production: if the 15-min access token is expired (or expiring within 60s),
            // try a silent refresh with the stored refresh token before declaring anonymous.
            if (IsTokenExpired(token, TimeSpan.FromSeconds(60)))
            {
                token = await TrySilentRefreshAsync() ?? "";
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

    private async Task<string?> TrySilentRefreshAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            using var response = await _http.PostAsJsonAsync("auth/refresh", new { refreshToken });
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var ok) || !ok.GetBoolean()) return null;
            if (!root.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("accessToken", out var at) || !data.TryGetProperty("refreshToken", out var rt)) return null;

            var newAccess = at.GetString();
            var newRefresh = rt.GetString();
            if (string.IsNullOrWhiteSpace(newAccess) || string.IsNullOrWhiteSpace(newRefresh)) return null;

            await _localStorage.SetItemAsync(AuthTokenKey, newAccess);
            await _localStorage.SetItemAsync(RefreshTokenKey, newRefresh);
            NotifyUserAuthentication(newAccess);
            return newAccess;
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsTokenExpired(string jwt, TimeSpan skew)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return true;
            var payload = parts[1];
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var bytes = Convert.FromBase64String(payload);
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
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

        if (keyValuePairs == null) return claims;

        foreach (var kvp in keyValuePairs)
        {
            var key = kvp.Key;

            // Normalize the role claim type so Blazor IsInRole and [Authorize(Roles="...")] work correctly
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

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
