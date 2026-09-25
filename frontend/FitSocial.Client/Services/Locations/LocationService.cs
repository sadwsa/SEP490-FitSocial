using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Locations;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Locations;

public interface ILocationService
{
    Task<ApiResponse<PagedResult<LocationDto>>> GetLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10);

    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync();

    Task<ApiResponse<bool>> DeleteLocationAsync(Guid locationId);
}

public class LocationService : ILocationService
{
    private readonly ApiClient _apiClient;

    public LocationService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<LocationDto>>> GetLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"pageNumber={pageNumber}",
                $"pageSize={pageSize}"
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            var endpoint = $"locations?{string.Join("&", queryParams)}";
            return await _apiClient.GetAsync<PagedResult<LocationDto>>(endpoint);
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<LocationDto>>
            {
                Success = false,
                Message = $"Error retrieving locations: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<List<LocationDto>>> GetLocationsAsync()
    {
        try
        {
            return await _apiClient.GetAsync<List<LocationDto>>("locations/public");
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<LocationDto>>
            {
                Success = false,
                Message = $"Error retrieving locations: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<bool>> DeleteLocationAsync(Guid locationId)
    {
        try
        {
            return await _apiClient.DeleteAsync<bool>($"locations/{locationId}");
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Error deleting location: {ex.Message}"
            };
        }
    }
}
