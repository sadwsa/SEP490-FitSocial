using System.Net.Http.Json;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Locations;

namespace FitSocial.Client.Services.Locations;

public interface ILocationService
{
    Task<ApiResponse<List<LocationDto>>> GetLocationsAsync();
}

public class LocationService : ILocationService
{
    private readonly HttpClient _httpClient;

    public LocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponse<List<LocationDto>>> GetLocationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("locations");
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
}
