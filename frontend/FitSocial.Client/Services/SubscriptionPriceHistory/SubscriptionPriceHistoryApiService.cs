using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SubscriptionPriceHistory;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.SubscriptionPriceHistory;

public class SubscriptionPriceHistoryApiService : ISubscriptionPriceHistoryApiService
{
    private readonly ApiClient _apiClient;

    public SubscriptionPriceHistoryApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<SubscriptionPriceHistoryDto>>> GetPriceHistoryAsync(GetPriceHistoryFilterRequest request)
    {
        request ??= new GetPriceHistoryFilterRequest();

        var queryParams = new List<string>
        {
            $"pageIndex={Math.Max(1, request.PageIndex)}",
            $"pageSize={Math.Clamp(request.PageSize, 1, 100)}"
        };

        if (request.PlanId.HasValue && request.PlanId.Value != Guid.Empty)
        {
            queryParams.Add($"planId={request.PlanId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            queryParams.Add($"keyword={Uri.EscapeDataString(request.Keyword.Trim())}");
        }

        if (request.FromDate.HasValue)
        {
            queryParams.Add($"fromDate={Uri.EscapeDataString(request.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        }

        if (request.ToDate.HasValue)
        {
            queryParams.Add($"toDate={Uri.EscapeDataString(request.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        }

        var endpoint = $"admin/subscription-plans/price-history?{string.Join("&", queryParams)}";
        return await _apiClient.GetAsync<PagedResult<SubscriptionPriceHistoryDto>>(endpoint);
    }
}
