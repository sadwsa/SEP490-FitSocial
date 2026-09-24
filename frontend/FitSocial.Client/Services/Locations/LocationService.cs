using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Locations;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Locations;

public interface ILocationService
{
    Task<ApiResponse<PagedResult<LocationDto>>> GetPagedLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10);

    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync();

    Task<ApiResponse<LocationDto>> CreateLocationAsync(CreateLocationDto dto);
}

public class LocationService : ILocationService
{
    private readonly HttpClient _httpClient;
    private readonly ApiClient _apiClient;

    public LocationService(HttpClient httpClient, ApiClient apiClient)
    {
        _httpClient = httpClient;
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<LocationDto>>> GetPagedLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            var query = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            var endpoint = "locations?" + string.Join("&", query);
            var result = await _apiClient.GetAsync<PagedResult<LocationDto>>(endpoint);
            if (result.Success && result.Data != null)
            {
                result.Data.Items ??= new List<LocationDto>();
            }
            return result;
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<LocationDto>>
            {
                Success = false,
                Message = $"Unable to load locations: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<List<LocationDto>>> GetLocationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("locations/public");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<LocationDto>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                result.Data ??= new List<LocationDto>();
                return result;
            }

            return result ?? new ApiResponse<List<LocationDto>>
            {
                Success = false,
                Message = $"Failed to load locations (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<LocationDto>>
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<LocationDto>> CreateLocationAsync(CreateLocationDto dto)
    {
        try
        {
            return await _apiClient.PostAsync<CreateLocationDto, LocationDto>("locations", dto);
        }
        catch (Exception ex)
        {
            return new ApiResponse<LocationDto>
            {
                Success = false,
                Message = $"Error creating location: {ex.Message}"
            };
        }
    }
}
