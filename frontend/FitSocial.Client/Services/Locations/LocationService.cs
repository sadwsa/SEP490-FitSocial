using System;
using System.Collections.Generic;
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

    Task<ApiResponse<PagedResult<LocationDto>>> GetLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10);

    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync();

    Task<ApiResponse<LocationDto>> CreateLocationAsync(CreateLocationDto dto);

    Task<ApiResponse<LocationDto>> UpdateLocationAsync(Guid locationId, UpdateLocationDto dto);

    Task<ApiResponse<bool>> DeleteLocationAsync(Guid locationId);
}

public class LocationService : ILocationService
{
    private readonly ApiClient _apiClient;

    public LocationService(ApiClient apiClient)
    {
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

    public Task<ApiResponse<PagedResult<LocationDto>>> GetLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        return GetPagedLocationsAsync(searchTerm, pageNumber, pageSize);
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

    public async Task<ApiResponse<LocationDto>> UpdateLocationAsync(Guid locationId, UpdateLocationDto dto)
    {
        try
        {
            return await _apiClient.PutAsync<UpdateLocationDto, LocationDto>($"locations/{locationId}", dto);
        }
        catch (Exception ex)
        {
            return new ApiResponse<LocationDto>
            {
                Success = false,
                Message = $"Error updating location: {ex.Message}"
            };
        }
    }
}
