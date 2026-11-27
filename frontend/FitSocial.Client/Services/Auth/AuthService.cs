using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;
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
    Task<ApiResponse<AuthResponse>> RegisterTraineeAsync(RegisterTraineeRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
    Task PersistLoginAsync(AuthResponse data);
    Task<bool> RefreshTokenAsync();
    Task<ApiResponse<UserInfo>> GetCurrentUserAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ApiClient _apiClient;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly ITokenStorage _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthService> _logger;
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public AuthService(
        HttpClient httpClient,
        ApiClient apiClient,
        ITokenStorage localStorage,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _apiClient = apiClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var result = await _apiClient.PostAsync<LoginRequest, AuthResponse>("auth/login", request);
        if (result.Success && result.Data != null)
        {
            await PersistAuthAsync(result.Data);
        }
        return result;
    }

    public async Task<ApiResponse<AuthResponse>> AdminLoginAsync(LoginRequest request)
    {
        var result = await _apiClient.PostAsync<LoginRequest, AuthResponse>("auth/admin-login", request);
        if (result.Success && result.Data != null)
        {
            await PersistAuthAsync(result.Data);
        }
        return result;
    }

    public async Task<ApiResponse<bool>> SendOtpAsync(SendOtpRequest request)
    {
        return await _apiClient.PostAsync<SendOtpRequest, bool>("auth/send-otp", request);
    }

    public async Task<ApiResponse<bool>> VerifyOtpAsync(string email, string otpCode, string purpose)
    {
        return await _apiClient.PostAsync<object, bool>("auth/verify-otp", new { email, otpCode, purpose });
    }

    public async Task<ApiResponse<bool>> ForgotPasswordAsync(string email)
    {
        return await _apiClient.PostAsync<object, bool>("auth/forgot-password", new { email });
    }

    public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return await _apiClient.PostAsync<ResetPasswordRequest, bool>("auth/reset-password", request);
    }

    public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordRequest request)
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

        return await _apiClient.PostAsync<ChangePasswordRequest, bool>("auth/change-password", request);
    }

    public async Task<ApiResponse<AuthResponse>> RegisterTraineeAsync(RegisterTraineeRequest request)
    {
        var result = await _apiClient.PostAsync<RegisterTraineeRequest, AuthResponse>("auth/register-trainee", request);
        if (result.Success && result.Data != null)
        {
            await PersistAuthAsync(result.Data);
        }
        return result;
    }
    public async Task PersistLoginAsync(AuthResponse data)
    {
        await PersistAuthAsync(data);
    }

    public async Task<ApiResponse<AuthResponse>> LoginWithGoogleCodeAsync(string code, string? roleCode = null, string? redirectUri = null)
    {
        var result = await _apiClient.PostAsync<object, AuthResponse>("auth/google/code", new { code, roleCode, redirectUri });
        if (result.Success && result.Data != null)
        {
            await PersistAuthAsync(result.Data);
        }
        return result;
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

    public async Task LogoutAsync()
    {
        string? refreshToken = null;
        try
        {
            refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            await _apiClient.PostAsync<object, bool>("auth/logout", new { refreshToken });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Server-side logout failed. Continuing with local sign-out.");
        }

        await _localStorage.RemoveItemAsync(AuthTokenKey);
        await _localStorage.RemoveItemAsync(RefreshTokenKey);
        ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserLogout();
    }

    public async Task<bool> RefreshTokenAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var currentToken = await _localStorage.GetItemAsync<string>(AuthTokenKey);
            if (!string.IsNullOrWhiteSpace(currentToken) && !CustomAuthenticationStateProvider.IsTokenExpired(currentToken, TimeSpan.FromSeconds(60)))
            {
                return true;
            }

            var refreshToken = await _localStorage.GetItemAsync<string>(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var response = await _httpClient.PostAsJsonAsync("auth/refresh", new { refreshToken });
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (result != null && result.Success && result.Data != null)
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
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(AuthTokenKey);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return false;

        if (!CustomAuthenticationStateProvider.IsTokenExpired(token, TimeSpan.FromSeconds(60)))
            return true;

        return await RefreshTokenAsync();
    }

    public async Task<ApiResponse<UserInfo>> GetCurrentUserAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ApiResponse<UserInfo>
            {
                Success = false,
                Message = "No authentication token found"
            };
        }

        return await _apiClient.GetAsync<UserInfo>("auth/me");
    }
}
