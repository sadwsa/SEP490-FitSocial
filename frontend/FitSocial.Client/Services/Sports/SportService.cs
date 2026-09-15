using System.Net.Http.Json;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Sports;

namespace FitSocial.Client.Services.Sports;

public interface ISportService
{
    Task<ApiResponse<List<SportDto>>> GetSportsAsync();
}

public class SportService : ISportService
{
    private readonly HttpClient _httpClient;

    public SportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponse<List<SportDto>>> GetSportsAsync()
    {
        try
        {
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
                Message = $"Không tải được danh sách môn thể thao (Mã: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<SportDto>>
            {
                Success = false,
                Message = $"Lỗi kết nối: {ex.Message}"
            };
        }
    }
}
