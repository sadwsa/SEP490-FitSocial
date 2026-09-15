using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace FitSocial.Client.Services.Auth;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _localStorage;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private const string AuthTokenKey = "authToken";

    public CustomAuthenticationStateProvider(ITokenStorage localStorage)
    {
        _localStorage = localStorage;
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
