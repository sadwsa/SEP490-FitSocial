using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPackages;
using FitSocial.Client.Services.Http;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.TrainingPackages;

public class TrainingPackageService : ITrainingPackageService
{
    private readonly ApiClient _apiClient;
    private const string BaseEndpoint = "trainingpackages"; // Phải khớp với [Route("api/trainingpackages")] ở Backend

    public TrainingPackageService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetMyPackagesAsync()
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}/my-packages");
    }

    public async Task<ApiResponse<TrainingPackageResponseDto>> GetPackageByIdAsync(Guid id)
    {
        return await _apiClient.GetAsync<TrainingPackageResponseDto>($"{BaseEndpoint}/{id}");
    }

    public async Task<ApiResponse<TrainingPackageResponseDto>> CreatePackageAsync(CreateTrainingPackageDto dto)
    {
        return await _apiClient.PostAsync<CreateTrainingPackageDto, TrainingPackageResponseDto>(BaseEndpoint, dto);
    }
    public async Task<ApiResponse<bool>> DeletePackageAsync(Guid id)
{
    return await _apiClient.DeleteAsync<bool>($"{BaseEndpoint}/{id}");
}
    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetPurchasedPackagesAsync()
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}/purchased");
    }


 

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetPackagesByCoachIdAsync(Guid coachId)
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}?coachId={coachId}");
    }public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchKeyword)) queryParams.Add($"searchKeyword={Uri.EscapeDataString(searchKeyword)}");
        if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
        if (coachId.HasValue) queryParams.Add($"coachId={coachId.Value}");
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}{query}");
    }
}
