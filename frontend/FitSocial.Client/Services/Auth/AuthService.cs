using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace FitSocial.Client.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> AdminLoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> LoginWithGoogleCodeAsync(string code, string? roleCode = null, string? redirectUri = null);
    Task<ApiResponse<bool>> SendOtpAsync(SendOtpRequest request);
    Task<ApiResponse<bool>> VerifyOtpAsync(string email, string otpCode, string purpose);
    Task<ApiResponse<bool>> ForgotPasswordAsync(string email);
    Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
    Task PersistLoginAsync(AuthResponse data);
    Task<bool> RefreshTokenAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthService> _logger;
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public AuthService(
        HttpClient httpClient,
        ITokenStorage localStorage,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        return await LoginInternalAsync("auth/login", request);
    }

    public async Task<ApiResponse<AuthResponse>> AdminLoginAsync(LoginRequest request)
    {
        return await LoginInternalAsync("auth/admin-login", request);
    }

    private async Task<ApiResponse<AuthResponse>> LoginInternalAsync(string endpoint, LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request);
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

    public async Task<ApiResponse<bool>> VerifyOtpAsync(string email, string otpCode, string purpose)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/verify-otp", new { email, otpCode, purpose });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null) return result;
            return new ApiResponse<bool> { Success = false, Message = $"Verification failed (Code: {response.StatusCode})" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool> { Success = false, Message = $"Connection error: {ex.Message}" };
        }
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(string email)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/forgot-password", new { email });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Request failed (Code: {response.StatusCode})"
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

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/reset-password", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Request failed (Code: {response.StatusCode})"
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

    public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
            if (string.IsNullOrWhiteSpace(token))
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Your session has expired. Please sign in again."
                };
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "auth/change-password");
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(httpRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Request failed (Code: {response.StatusCode})"
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
    private async Task<(ApiResponse<T>? Result, string? RawPreview)> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        string raw;
        try
        {
            raw = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the response body.");
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Response body is not the expected JSON shape. Preview: {Preview}", preview);
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
        // Best effort: revoke the tokens server-side first so they die immediately.
        // Local sign-out below always runs, even offline.
        string? refreshToken = null;
        try
        {
            var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
            refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout");
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    request.Content = JsonContent.Create(new { refreshToken });
                }
                await _httpClient.SendAsync(request);
            }
        }
        catch (Exception ex)
        {
            // Ignored on purpose: local sign-out must never be blocked by network errors.
            _logger.LogWarning(ex, "Server-side logout failed. Continuing with local sign-out.");
        }

        _httpClient.DefaultRequestHeaders.Authorization = null;
        await _localStorage.RemoveItemAsync(AuthTokenKey);
        await _localStorage.RemoveItemAsync(RefreshTokenKey);
        ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserLogout();
    }

    /// <summary>
    /// Rotates the session using the stored refresh token.
    /// Returns false when there is no refresh token or the server rejects it
    /// (caller should send the user back to the login page).
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync("auth/refresh", new { refreshToken });
            var (result, _) = await ReadResponseAsync<AuthResponse>(response);
            if (response.IsSuccessStatusCode && result != null && result.Success && result.Data != null)
            {
                await PersistAuthAsync(result.Data);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Silent token refresh failed.");
            return false;
        }
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
