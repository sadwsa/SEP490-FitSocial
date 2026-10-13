using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPlans;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.TrainingPlans;

public interface ITrainingPlanClientService
{
    Task<ApiResponse<TrainingPlanDetailDto>> CreateTrainingPlanAsync(CreateTrainingPlanDto dto);
    Task<ApiResponse<TrainingPlanDetailDto>> UpdateTrainingPlanAsync(Guid planId, UpdateTrainingPlanDto dto);
    Task<ApiResponse<TrainingPlanDetailDto?>> GetTrainingPlanByOrderDetailAsync(Guid orderDetailsId);
    Task<ApiResponse<List<VideoTutorialDto>>> GetCoachVideoTutorialsAsync();
}

public class TrainingPlanClientService : ITrainingPlanClientService
{
    private readonly ApiClient _apiClient;

    public TrainingPlanClientService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<TrainingPlanDetailDto>> CreateTrainingPlanAsync(CreateTrainingPlanDto dto)
    {
        return await _apiClient.PostAsync<CreateTrainingPlanDto, TrainingPlanDetailDto>("coach/training-plans", dto);
    }

    public async Task<ApiResponse<TrainingPlanDetailDto>> UpdateTrainingPlanAsync(Guid planId, UpdateTrainingPlanDto dto)
    {
        return await _apiClient.PutAsync<UpdateTrainingPlanDto, TrainingPlanDetailDto>($"coach/training-plans/{planId}", dto);
    }

    public async Task<ApiResponse<TrainingPlanDetailDto?>> GetTrainingPlanByOrderDetailAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<TrainingPlanDetailDto?>($"coach/training-plans/order-detail/{orderDetailsId}");
    }

    public async Task<ApiResponse<List<VideoTutorialDto>>> GetCoachVideoTutorialsAsync()
    {
        return await _apiClient.GetAsync<List<VideoTutorialDto>>("coach/training-plans/video-tutorials");
    }
}
