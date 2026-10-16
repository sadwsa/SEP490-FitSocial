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
}
