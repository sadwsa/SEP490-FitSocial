using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Sports;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Sports;

public interface ISportService
{
    Task<ApiResponse<List<SportDto>>> GetSportsAsync();
    Task<ApiResponse<SportDto>> CreateSportAsync(CreateSportDto dto);
    Task<ApiResponse<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto);
    Task<ApiResponse<bool>> DeleteSportAsync(Guid sportId);
    Task<ApiResponse<List<SportDto>>> GetPublicSportsAsync();
}

public class SportService : ISportService
{
    private readonly ApiClient _apiClient;

    public SportService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<List<SportDto>>> GetSportsAsync()
    {
        return await _apiClient.GetAsync<List<SportDto>>("sports");
    }

    public async Task<ApiResponse<SportDto>> CreateSportAsync(CreateSportDto dto)
    {
        return await _apiClient.PostAsync<CreateSportDto, SportDto>("sports", dto);
    }

    public async Task<ApiResponse<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto)
    {
        return await _apiClient.PutAsync<UpdateSportDto, SportDto>($"sports/{sportId}", dto);
    }

    public async Task<ApiResponse<bool>> DeleteSportAsync(Guid sportId)
    {
        return await _apiClient.DeleteAsync<bool>($"sports/{sportId}");
    }

    public async Task<ApiResponse<List<SportDto>>> GetPublicSportsAsync()
    {
        return await _apiClient.GetAsync<List<SportDto>>("sports/public");
    }
}
