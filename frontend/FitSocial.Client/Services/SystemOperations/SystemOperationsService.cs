using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SystemOperations;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.SystemOperations;

public interface ISystemOperationsService
{
    Task<ApiResponse<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(bool? isActive = null, string? search = null, int page = 1, int pageSize = 20);
    Task<ApiResponse<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId);
    Task<ApiResponse<CoachSubscriptionPlanDto>> CreatePlanAsync(CreatePlanRequest request);
    Task<ApiResponse<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid planId, UpdatePlanRequest request);
    Task<ApiResponse<bool>> DeletePlanAsync(Guid planId);
    Task<ApiResponse<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync();
}

public class SystemOperationsService : ISystemOperationsService
{
    private readonly ApiClient _apiClient;

    public SystemOperationsService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(bool? isActive = null, string? search = null, int page = 1, int pageSize = 20)
    {
        var q = new List<string>();
        if (isActive.HasValue) q.Add($"isActive={isActive.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        q.Add($"page={page}");
        q.Add($"pageSize={pageSize}");
        var url = $"system-operations/coach-subscription-plans?{string.Join("&", q)}";
        return await _apiClient.GetAsync<CoachSubscriptionPlansResponseDto>(url);
    }

    public Task<ApiResponse<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId)
        => _apiClient.GetAsync<CoachSubscriptionPlanDto>($"system-operations/coach-subscription-plans/{priceId}");

    public Task<ApiResponse<CoachSubscriptionPlanDto>> CreatePlanAsync(CreatePlanRequest request)
        => _apiClient.PostAsync<CreatePlanRequest, CoachSubscriptionPlanDto>("system-operations/coach-subscription-plans", request);

    public Task<ApiResponse<CoachSubscriptionPlanDto>> UpdatePlanAsync(Guid planId, UpdatePlanRequest request)
        => _apiClient.PutAsync<UpdatePlanRequest, CoachSubscriptionPlanDto>($"system-operations/coach-subscription-plans/{planId}", request);

    public Task<ApiResponse<bool>> DeletePlanAsync(Guid planId)
        => _apiClient.DeleteAsync<bool>($"system-operations/coach-subscription-plans/{planId}");

    public Task<ApiResponse<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync()
        => _apiClient.GetAsync<List<CoachSubscriptionPlanDto>>("coachsubscriptionplans");
}