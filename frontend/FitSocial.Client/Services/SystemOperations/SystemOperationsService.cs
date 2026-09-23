using System.Net.Http.Json;
using System.Text.Json;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SystemOperations;

namespace FitSocial.Client.Services.SystemOperations;

public class CreatePlanRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
}

public interface ISystemOperationsService
{
    Task<ApiResponse<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(bool? isActive = null, string? search = null, int page = 1, int pageSize = 20);
    Task<ApiResponse<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId);
    Task<ApiResponse<CoachSubscriptionPlanDto>> CreatePlanAsync(CreatePlanRequest request);
    Task<ApiResponse<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync();
}

public class SystemOperationsService : ISystemOperationsService
{
    private readonly HttpClient _http;

    public SystemOperationsService(HttpClient http) => _http = http;

    public async Task<ApiResponse<CoachSubscriptionPlansResponseDto>> GetCoachSubscriptionPlansAsync(bool? isActive = null, string? search = null, int page = 1, int pageSize = 20)
    {
        var q = new List<string>();
        if (isActive.HasValue) q.Add($"isActive={isActive.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        q.Add($"page={page}");
        q.Add($"pageSize={pageSize}");
        var url = $"system-operations/coach-subscription-plans?{string.Join("&", q)}";
        try
        {
            var resp = await _http.GetAsync(url);
            var raw = await resp.Content.ReadAsStringAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var wrapped = JsonSerializer.Deserialize<ApiResponse<CoachSubscriptionPlansResponseDto>>(raw, opts);
            if (wrapped != null) return wrapped;
            return new ApiResponse<CoachSubscriptionPlansResponseDto> { Success = false, Message = "Failed to parse response." };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CoachSubscriptionPlansResponseDto> { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiResponse<CoachSubscriptionPlanDto>> GetPlanByIdAsync(Guid priceId)
    {
        try
        {
            var resp = await _http.GetAsync($"system-operations/coach-subscription-plans/{priceId}");
            var raw = await resp.Content.ReadAsStringAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var wrapped = JsonSerializer.Deserialize<ApiResponse<CoachSubscriptionPlanDto>>(raw, opts);
            if (wrapped != null) return wrapped;
            return new ApiResponse<CoachSubscriptionPlanDto> { Success = false, Message = "Failed to parse response." };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CoachSubscriptionPlanDto> { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiResponse<CoachSubscriptionPlanDto>> CreatePlanAsync(CreatePlanRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("system-operations/coach-subscription-plans", request);
            var raw = await resp.Content.ReadAsStringAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var wrapped = JsonSerializer.Deserialize<ApiResponse<CoachSubscriptionPlanDto>>(raw, opts);
            if (wrapped != null) return wrapped;
            return new ApiResponse<CoachSubscriptionPlanDto> { Success = false, Message = "Failed to parse response." };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CoachSubscriptionPlanDto> { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiResponse<List<CoachSubscriptionPlanDto>>> GetActivePlansAsync()
    {
        try
        {
            var resp = await _http.GetAsync("system-operations/coach-subscription-plans/active");
            var raw = await resp.Content.ReadAsStringAsync();
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // Backend returns {success, data: List<Plan>}
            var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                var list = JsonSerializer.Deserialize<List<CoachSubscriptionPlanDto>>(dataEl.GetRawText(), opts);
                return new ApiResponse<List<CoachSubscriptionPlanDto>> { Success = true, Data = list ?? new() };
            }
            var wrapped = JsonSerializer.Deserialize<ApiResponse<List<CoachSubscriptionPlanDto>>>(raw, opts);
            if (wrapped != null) return wrapped;
            return new ApiResponse<List<CoachSubscriptionPlanDto>> { Success = false, Message = "Failed to parse response." };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<CoachSubscriptionPlanDto>> { Success = false, Message = ex.Message };
        }
    }
}
