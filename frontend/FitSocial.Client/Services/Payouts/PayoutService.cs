using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Payouts;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Payouts;

/// <summary>
/// UC-23.1: Staff/Admin coach payout management (authenticated via ApiClient).
/// </summary>
public interface IPayoutService
{
    Task<ApiResponse<List<PayoutHistoryItem>>> GetHistoryAsync(int? month = null, int? year = null, string? status = null);
    Task<ApiResponse<GeneratePayoutsResult>> GenerateAsync(int month, int year);
    Task<ApiResponse<PayoutDetail>> GetDetailAsync(Guid payoutId);
    Task<ApiResponse<ProcessPayoutResult>> ProcessAsync(Guid payoutId);
}

public class PayoutApiService : IPayoutService
{
    private readonly ApiClient _apiClient;

    public PayoutApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<List<PayoutHistoryItem>>> GetHistoryAsync(int? month = null, int? year = null, string? status = null)
    {
        var query = new List<string>();
        if (month.HasValue) query.Add($"month={month.Value}");
        if (year.HasValue) query.Add($"year={year.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status.Trim().ToUpperInvariant())}");
        var endpoint = "payouts" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return _apiClient.GetAsync<List<PayoutHistoryItem>>(endpoint);
    }

    public Task<ApiResponse<GeneratePayoutsResult>> GenerateAsync(int month, int year)
    {
        return _apiClient.PostAsync<object, GeneratePayoutsResult>(
            "payouts/generate", new { month, year });
    }

    public Task<ApiResponse<PayoutDetail>> GetDetailAsync(Guid payoutId)
    {
        return _apiClient.GetAsync<PayoutDetail>($"payouts/{payoutId}");
    }

    public Task<ApiResponse<ProcessPayoutResult>> ProcessAsync(Guid payoutId)
    {
        return _apiClient.PostAsync<ProcessPayoutResult>($"payouts/{payoutId}/process");
    }
}
