using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Sports;
using FitSocial.Client.Services.Auth;

namespace FitSocial.Client.Services.Sports;

public interface ISportService
{
    Task<ApiResponse<List<SportDto>>> GetSportsAsync();
    Task<ApiResponse<List<SportDto>>> GetPublicSportsAsync();
    Task<ApiResponse<SportDto>> CreateSportAsync(CreateSportDto dto);
    Task<ApiResponse<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto);
    Task<ApiResponse<bool>> DeleteSportAsync(Guid sportId);
}

public class SportService : ISportService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private const string AuthTokenKey = "authToken";

    public SportService(HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
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

    public async Task<ApiResponse<List<SportDto>>> GetSportsAsync()
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _httpClient.GetAsync("sports");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SportDto>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                result.Data ??= new List<SportDto>();
                return result;
            }

            return result ?? new ApiResponse<List<SportDto>>
            {
                Success = false,
                Message = $"Unable to load sports list (Status: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<SportDto>>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<List<SportDto>>> GetPublicSportsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("sports/public");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SportDto>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                result.Data ??= new List<SportDto>();
                return result;
            }

            return result ?? new ApiResponse<List<SportDto>>
            {
                Success = false,
                Message = $"Unable to load sports list (Status: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<SportDto>>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<SportDto>> CreateSportAsync(CreateSportDto dto)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _httpClient.PostAsJsonAsync("sports", dto);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SportDto>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<SportDto>
            {
                Success = false,
                Message = $"Failed to create sport (Status: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<SportDto>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _httpClient.PutAsJsonAsync($"sports/{sportId}", dto);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SportDto>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<SportDto>
            {
                Success = false,
                Message = $"Failed to update sport (Status: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<SportDto>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteSportAsync(Guid sportId)
    {
        try
        {
            await AttachBearerTokenAsync();
            var response = await _httpClient.DeleteAsync($"sports/{sportId}");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<bool>
            {
                Success = false,
                Message = $"Failed to delete sport (Status: {response.StatusCode})"
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
