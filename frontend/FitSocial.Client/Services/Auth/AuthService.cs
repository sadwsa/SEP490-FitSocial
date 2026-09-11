using System.Net.Http.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using Microsoft.AspNetCore.Components.Authorization;

namespace FitSocial.Client.Services.Auth;

public interface IAuthService
{
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
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
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
                if (result?.Data != null && !string.IsNullOrEmpty(result.Data.AccessToken))
                {
                    await _localStorage.SetItemAsync(AuthTokenKey, result.Data.AccessToken);
                    await _localStorage.SetItemAsync(RefreshTokenKey, result.Data.RefreshToken);
                    ((CustomAuthenticationStateProvider)_authStateProvider).NotifyUserAuthentication(result.Data.AccessToken);
                }
                return result ?? new ApiResponse<AuthResponse> { Success = false, Message = "Phản hồi không hợp lệ" };
            }

            return new ApiResponse<AuthResponse>
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

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
                return result ?? new ApiResponse<AuthResponse> { Success = false, Message = "Phản hồi không hợp lệ" };
            }

            return new ApiResponse<AuthResponse>
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
