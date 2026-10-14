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

    // Trainee Workout Hub & Daily Workout Marking
    Task<ApiResponse<TraineeWorkoutPlanProgressDto>> GetTraineeWorkoutPlanAsync(Guid orderDetailsId);
    Task<ApiResponse<TraineeWorkoutPlanProgressDto>> GetTraineeWorkoutPlanByPackageAsync(Guid packageId);
    Task<ApiResponse<WorkoutCompletionResultDto>> ToggleWorkoutCompletionAsync(ToggleWorkoutCompletionDto dto);

    // Coach Progress Monitoring
    Task<ApiResponse<CoachTraineeWorkoutProgressDto>> GetCoachTraineeProgressAsync(Guid orderDetailsId);
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

    public async Task<ApiResponse<TraineeWorkoutPlanProgressDto>> GetTraineeWorkoutPlanAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<TraineeWorkoutPlanProgressDto>($"trainee/workout/plan/{orderDetailsId}");
    }

    public async Task<ApiResponse<TraineeWorkoutPlanProgressDto>> GetTraineeWorkoutPlanByPackageAsync(Guid packageId)
    {
        return await _apiClient.GetAsync<TraineeWorkoutPlanProgressDto>($"trainee/workout/plan/by-package/{packageId}");
    }

    public async Task<ApiResponse<WorkoutCompletionResultDto>> ToggleWorkoutCompletionAsync(ToggleWorkoutCompletionDto dto)
    {
        return await _apiClient.PostAsync<ToggleWorkoutCompletionDto, WorkoutCompletionResultDto>("trainee/workout/complete", dto);
    }

    public async Task<ApiResponse<CoachTraineeWorkoutProgressDto>> GetCoachTraineeProgressAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<CoachTraineeWorkoutProgressDto>($"coach/training-plans/trainee-progress/{orderDetailsId}");
    }
}
