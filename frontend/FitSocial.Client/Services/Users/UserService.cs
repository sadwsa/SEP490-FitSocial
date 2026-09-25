using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Users;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Users;

public interface IUserService
{
    Task<ApiResponse<PagedResult<AdminUserDto>>> GetUsersAsync(
        string? search = null,
        string? role = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10);
    Task<ApiResponse<bool>> SetUserLockStatusAsync(Guid userId, bool isLocked);
    Task<ApiResponse<UserProfileModel>> GetOwnProfileAsync();
    Task<ApiResponse<OtherUserProfileModel>> GetUserProfileAsync(Guid userId);
}

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly ApiClient _apiClient;
    private const string AuthTokenKey = "authToken";

    public UserService(HttpClient httpClient, ITokenStorage tokenStorage, ApiClient apiClient)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<UserProfileModel>> GetOwnProfileAsync()
    {
        return await _apiClient.GetAsync<UserProfileModel>("users/me/profile");
    }

    public async Task<ApiResponse<OtherUserProfileModel>> GetUserProfileAsync(Guid userId)
    {
        return await _apiClient.GetAsync<OtherUserProfileModel>($"users/{userId}/profile");
    }

    private async Task AttachBearerTokenAsync()
    {
        var token = await _tokenStorage.GetItemAsync<string>(AuthTokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<ApiResponse<PagedResult<AdminUserDto>>> GetUsersAsync(
        string? search = null,
        string? role = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            await AttachBearerTokenAsync();
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                query.Add($"role={Uri.EscapeDataString(role.Trim())}");
            }
            if (isLocked.HasValue)
            {
                query.Add($"isLocked={isLocked.Value.ToString().ToLowerInvariant()}");
            }
            query.Add($"pageNumber={pageNumber}");
            query.Add($"pageSize={pageSize}");

            var endpoint = "admin/users";
            if (query.Count > 0)
            {
                endpoint += "?" + string.Join("&", query);
            }

            var response = await _httpClient.GetAsync(endpoint);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<AdminUserDto>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                result.Data ??= new PagedResult<AdminUserDto>();
                return result;
            }

            return result ?? new ApiResponse<PagedResult<AdminUserDto>>
            {
                Success = false,
                Message = $"Unable to load user accounts (Status: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<AdminUserDto>>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<bool>> SetUserLockStatusAsync(Guid userId, bool isLocked)
    {
        try
        {
            await AttachBearerTokenAsync();
            var request = new UpdateUserLockStatusRequest { IsLocked = isLocked };
            var response = await _httpClient.PutAsJsonAsync($"admin/users/{userId}/lock", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<bool>
            {
                Success = false,
                Message = $"Failed to update lock status (Status: {response.StatusCode})"
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
}
