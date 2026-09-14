using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using Microsoft.AspNetCore.Components.Authorization;

namespace FitSocial.Client.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> LoginWithGoogleCodeAsync(string code, string? roleCode = null, string? redirectUri = null);
    Task<ApiResponse<bool>> SendOtpAsync(SendOtpRequest request);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
    Task PersistLoginAsync(AuthResponse data);
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public AuthService(
        HttpClient httpClient,
        ITokenStorage localStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                if (result.Data != null && !string.IsNullOrEmpty(result.Data.AccessToken))
                {
                    await _localStorage.SetItemAsync(AuthTokenKey, result.Data.AccessToken);
                    await _localStorage.SetItemAsync(RefreshTokenKey, result.Data.RefreshToken);
                    ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserAuthentication(result.Data.AccessToken);
                }
                return result;
            }

            return result ?? new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Sign-in failed (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<bool>> SendOtpAsync(SendOtpRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/send-otp", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<bool>
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode ? "OTP code sent successfully" : $"Failed to send OTP code (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register-trainee", request);
            var (result, rawPreview) = await ReadResponseAsync<AuthResponse>(response);
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                if (result.Data != null && !string.IsNullOrEmpty(result.Data.AccessToken))
                {
                    await _localStorage.SetItemAsync(AuthTokenKey, result.Data.AccessToken);
                    await _localStorage.SetItemAsync(RefreshTokenKey, result.Data.RefreshToken);
                    ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserAuthentication(result.Data.AccessToken);
                }
                return result;
            }

            if (result != null)
            {
                return result;
            }

            return UnexpectedResponse<AuthResponse>(
                response, rawPreview, $"Registration failed (Code: {response.StatusCode})");
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }
    public async Task PersistLoginAsync(AuthResponse data)
    {
        await PersistAuthAsync(data);
    }

    public async Task<ApiResponse<AuthResponse>> LoginWithGoogleCodeAsync(string code, string? roleCode = null, string? redirectUri = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/google/code", new { code, roleCode, redirectUri });
            var (result, rawPreview) = await ReadResponseAsync<AuthResponse>(response);
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                if (result.Data != null)
                {
                    await PersistAuthAsync(result.Data);
                }
                return result;
            }

            if (result != null)
            {
                return result;
            }

            return UnexpectedResponse<AuthResponse>(
                response, rawPreview, $"Google sign-in failed (Code: {response.StatusCode})");
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    private async Task PersistAuthAsync(AuthResponse data)
    {
        if (!string.IsNullOrEmpty(data.AccessToken))
        {
            await _localStorage.SetItemAsync(AuthTokenKey, data.AccessToken);
            await _localStorage.SetItemAsync(RefreshTokenKey, data.RefreshToken);
            ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserAuthentication(data.AccessToken);
        }
    }

    /// <summary>
    /// Reads the raw body first so a non-JSON server reply (proxy page, crash page, ...)
    /// surfaces its real content instead of a cryptic "'X' is an invalid start" error.
    /// </summary>
    private static async Task<(ApiResponse<T>? Result, string? RawPreview)> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        string raw;
        try
        {
            raw = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var preview = raw.Length > 300 ? raw[..300] : raw;
        try
        {
            var parsed = JsonSerializer.Deserialize<ApiResponse<T>>(
                raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return (parsed, preview);
        }
        catch
        {
            return (null, preview);
        }
    }

    private static ApiResponse<T> UnexpectedResponse<T>(HttpResponseMessage response, string? rawPreview, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(rawPreview))
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Unexpected server response (HTTP {(int)response.StatusCode}): {rawPreview}"
            };
        }

        return new ApiResponse<T> { Success = false, Message = fallback };
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(AuthTokenKey);
        await _localStorage.RemoveItemAsync(RefreshTokenKey);
        ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserLogout();
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(AuthTokenKey);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}
