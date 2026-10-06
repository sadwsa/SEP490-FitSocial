using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FitSocial.Client.Models.Analytics;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;
using Microsoft.JSInterop;

namespace FitSocial.Client.Services.Analytics;

public class RevenueAnalyticsService : IRevenueAnalyticsService
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _http;
    private readonly ITokenStorage _tokenStorage;
    private readonly IJSRuntime _jsRuntime;

    public RevenueAnalyticsService(
        ApiClient apiClient,
        HttpClient http,
        ITokenStorage tokenStorage,
        IJSRuntime jsRuntime)
    {
        _apiClient = apiClient;
        _http = http;
        _tokenStorage = tokenStorage;
        _jsRuntime = jsRuntime;
    }

    public Task<ApiResponse<FullRevenueAnalyticsResponseModel>> GetRevenueOverviewAsync(RevenueAnalyticsFilterModel filter)
    {
        var query = BuildQueryString(filter);
        var endpoint = $"admin/analytics/revenue/overview{query}";
        return _apiClient.GetAsync<FullRevenueAnalyticsResponseModel>(endpoint);
    }

    public async Task<bool> ExportRevenueCsvAsync(RevenueAnalyticsFilterModel filter)
    {
        try
        {
            var query = BuildQueryString(filter);
            var endpoint = $"admin/analytics/revenue/export-csv{query}";

            var token = await _tokenStorage.GetItemAsync<string>("authToken");
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (!string.IsNullOrWhiteSpace(token))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return false;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            var fileName = $"FitSocial_Revenue_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

            var js = $@"
                (function() {{
                    var byteCharacters = atob('{base64}');
                    var byteNumbers = new Array(byteCharacters.length);
                    for (var i = 0; i < byteCharacters.length; i++) {{
                        byteNumbers[i] = byteCharacters.charCodeAt(i);
                    }}
                    var byteArray = new Uint8Array(byteNumbers);
                    var blob = new Blob([byteArray], {{type: 'text/csv;charset=utf-8;'}});
                    var link = document.createElement('a');
                    link.href = URL.createObjectURL(blob);
                    link.download = '{fileName}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                }})();
            ";

            await _jsRuntime.InvokeVoidAsync("eval", js);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RevenueAnalytics] Export error: {ex.Message}");
            return false;
        }
    }

    private static string BuildQueryString(RevenueAnalyticsFilterModel filter)
    {
        var q = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.QuickFilter))
        {
            q.Add($"quickFilter={Uri.EscapeDataString(filter.QuickFilter.Trim())}");
        }

        if (filter.FromDate.HasValue)
        {
            q.Add($"fromDate={filter.FromDate.Value:yyyy-MM-dd}");
        }

        if (filter.ToDate.HasValue)
        {
            q.Add($"toDate={filter.ToDate.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(filter.Granularity))
        {
            q.Add($"granularity={Uri.EscapeDataString(filter.Granularity.Trim().ToUpperInvariant())}");
        }

        return q.Count > 0 ? "?" + string.Join("&", q) : string.Empty;
    }
}
