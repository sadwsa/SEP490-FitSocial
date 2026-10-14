using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.MealPlans;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.MealPlans;

public interface IMealPlanClientService
{
    Task<ApiResponse<TraineeMealPlanModel?>> GetTraineeMealPlanAsync(Guid orderDetailsId);
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
}
