using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPackages;
using FitSocial.Client.Services.Http;
using System;
using System.Collections.Generic;
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
}
