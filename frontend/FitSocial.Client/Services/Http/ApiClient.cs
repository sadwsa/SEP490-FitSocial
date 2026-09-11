using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Http;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private const string AuthTokenKey = "authToken";

    public ApiClient(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
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

    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _http.GetAsync(endpoint);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>();
                return new ApiResponse<T> { Success = true, Data = data };
            }

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
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TResult>();
                return new ApiResponse<TResult> { Success = true, Data = data };
            }

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
