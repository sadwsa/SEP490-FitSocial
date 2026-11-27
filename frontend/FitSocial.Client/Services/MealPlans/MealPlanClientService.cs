using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.MealPlans;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.MealPlans;

public interface IMealPlanClientService
{
    Task<ApiResponse<TraineeMealPlanModel?>> GetTraineeMealPlanAsync(Guid orderDetailsId);
    Task<ApiResponse<TraineeMealPlanModel>> CreateMealPlanAsync(CreateMealPlanDto dto);
    Task<ApiResponse<TraineeMealPlanModel>> UpdateMealPlanAsync(Guid planId, UpdateMealPlanDto dto);
    Task<ApiResponse<TraineeMealPlanModel?>> GetCoachMealPlanByOrderDetailAsync(Guid orderDetailsId);
    Task<ApiResponse<MealCompletionResultDto>> RequestMealCompletionAsync(RequestMealCompletionDto dto);
    Task<ApiResponse<MealCompletionResultDto>> SaveMealMissedReasonAsync(SaveMealMissedReasonDto dto);
    Task<ApiResponse<CoachTraineeMealProgressDto>> GetCoachTraineeMealProgressAsync(Guid orderDetailsId);
    Task<ApiResponse<MealCompletionResultDto>> ReviewMealCompletionAsync(CoachReviewMealDto dto);
}

public class MealPlanClientService : IMealPlanClientService
{
    private readonly ApiClient _apiClient;

    public MealPlanClientService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<TraineeMealPlanModel?>> GetTraineeMealPlanAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<TraineeMealPlanModel?>($"trainee/meal-plan/{orderDetailsId}");
    }

    public async Task<ApiResponse<TraineeMealPlanModel>> CreateMealPlanAsync(CreateMealPlanDto dto)
    {
        return await _apiClient.PostAsync<CreateMealPlanDto, TraineeMealPlanModel>("coach/meal-plans", dto);
    }

    public async Task<ApiResponse<TraineeMealPlanModel>> UpdateMealPlanAsync(Guid planId, UpdateMealPlanDto dto)
    {
        return await _apiClient.PutAsync<UpdateMealPlanDto, TraineeMealPlanModel>($"coach/meal-plans/{planId}", dto);
    }

    public async Task<ApiResponse<TraineeMealPlanModel?>> GetCoachMealPlanByOrderDetailAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<TraineeMealPlanModel?>($"coach/meal-plans/order-detail/{orderDetailsId}");
    }

    public async Task<ApiResponse<MealCompletionResultDto>> RequestMealCompletionAsync(RequestMealCompletionDto dto)
    {
        return await _apiClient.PostAsync<RequestMealCompletionDto, MealCompletionResultDto>("trainee/meal-plan/request-completion", dto);
    }

    public async Task<ApiResponse<MealCompletionResultDto>> SaveMealMissedReasonAsync(SaveMealMissedReasonDto dto)
    {
        return await _apiClient.PostAsync<SaveMealMissedReasonDto, MealCompletionResultDto>("trainee/meal-plan/missed-reason", dto);
    }

    public async Task<ApiResponse<CoachTraineeMealProgressDto>> GetCoachTraineeMealProgressAsync(Guid orderDetailsId)
    {
        return await _apiClient.GetAsync<CoachTraineeMealProgressDto>($"coach/meal-plans/trainee-progress/{orderDetailsId}");
    }

    public async Task<ApiResponse<MealCompletionResultDto>> ReviewMealCompletionAsync(CoachReviewMealDto dto)
    {
        return await _apiClient.PostAsync<CoachReviewMealDto, MealCompletionResultDto>("coach/meal-plans/review-meal", dto);
    }
}
