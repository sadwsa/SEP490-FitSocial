using System.Net.Http.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using Microsoft.AspNetCore.Components.Authorization;

namespace FitSocial.Client.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> LoginWithGoogleAsync(string idToken);
    Task<ApiResponse<bool>> SendOtpAsync(SendOtpRequest request);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<bool> IsAuthenticatedAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private const string AuthTokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";

    public AuthService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
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
                Message = $"Đăng nhập thất bại (Mã: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Lỗi kết nối: {ex.Message}"
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
                Message = response.IsSuccessStatusCode ? "Đã gửi mã OTP thành công" : $"Gửi mã OTP thất bại (Mã: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Lỗi kết nối: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register-trainee", request);
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
                Message = $"Đăng ký thất bại (Mã: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Lỗi kết nối: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AuthResponse>> LoginWithGoogleAsync(string idToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/google", new GoogleLoginRequest { IdToken = idToken });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                if (result.Data != null)
                {
                    await PersistAuthAsync(result.Data);
                }
                return result;
            }

            return result ?? new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Đăng nhập Google thất bại (Mã: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Lỗi kết nối: {ex.Message}"
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
