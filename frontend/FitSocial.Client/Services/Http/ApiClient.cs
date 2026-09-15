using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Auth;

namespace FitSocial.Client.Services.Http;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenStorage _localStorage;
    private readonly IAuthService _authService;
    private const string AuthTokenKey = "authToken";

    public ApiClient(HttpClient http, ITokenStorage localStorage, IAuthService authService)
    {
        _http = http;
        _localStorage = localStorage;
        _authService = authService;
    }

    private async Task AttachBearerTokenAsync()
    {
        var token = await _localStorage.GetItemAsync<string>(AuthTokenKey);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static bool IsAuthEndpoint(string endpoint) =>
        endpoint.StartsWith("auth/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// On 401 tries one silent refresh (except for auth endpoints themselves),
    /// so short-lived access tokens never interrupt the user.
    /// </summary>
    private async Task<bool> TryRefreshOnceAsync(string endpoint, HttpStatusCode status)
    {
        if (status != HttpStatusCode.Unauthorized || IsAuthEndpoint(endpoint))
        {
            return false;
        }

        try
        {
            return await _authService.RefreshTokenAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.GetAsync(endpoint);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.GetAsync(endpoint);
            }
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    var data = await response.Content.ReadFromJsonAsync<T>();
                    return new ApiResponse<T> { Success = true, Data = data };
                }
            }

            try
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
                if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                {
                    return errorResponse;
                }
            }
            catch { }

            return new ApiResponse<T>
            {
                Success = false,
                Message = $"API Error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<TResult>> PostAsync<TRequest, TResult>(string endpoint, TRequest payload)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.PostAsJsonAsync(endpoint, payload);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.PostAsJsonAsync(endpoint, payload);
            }
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TResult>>();
                    if (apiResponse != null)
                    {
                        return apiResponse;
                    }
                }
                catch
                {
                    var data = await response.Content.ReadFromJsonAsync<TResult>();
                    return new ApiResponse<TResult> { Success = true, Data = data };
                }
            }

            try
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<ApiResponse<TResult>>();
                if (errorResponse != null && !string.IsNullOrWhiteSpace(errorResponse.Message))
                {
                    return errorResponse;
                }
            }
            catch { }

            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"API Error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TResult>
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse> DeleteAsync(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.DeleteAsync(endpoint);
            if (response.StatusCode == HttpStatusCode.Unauthorized &&
                await TryRefreshOnceAsync(endpoint, response.StatusCode))
            {
                await AttachBearerTokenAsync();
                response = await _http.DeleteAsync(endpoint);
            }
            return new ApiResponse
            {
                Success = response.IsSuccessStatusCode,
                Message = response.IsSuccessStatusCode ? null : $"API Error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Network Error: {ex.Message}"
            };
        }
    }
}
