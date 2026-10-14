using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SystemLogs;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.SystemLogs;

public class SystemLogService : ISystemLogService
{
    private readonly ApiClient _apiClient;

    public SystemLogService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<SystemLogItemModel>>> GetSystemLogsAsync(GetSystemLogsRequest request)
    {
        request ??= new GetSystemLogsRequest();

        var queryParams = new List<string>
        {
            $"pageNumber={Math.Max(1, request.PageNumber)}",
            $"pageSize={Math.Clamp(request.PageSize, 1, 100)}"
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            queryParams.Add($"level={Uri.EscapeDataString(request.Level.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            queryParams.Add($"source={Uri.EscapeDataString(request.Source.Trim())}");
        }

        if (request.FromDate.HasValue)
        {
            queryParams.Add($"fromDate={Uri.EscapeDataString(request.FromDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        }

        if (request.ToDate.HasValue)
        {
            queryParams.Add($"toDate={Uri.EscapeDataString(request.ToDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
        }

        var endpoint = $"admin/system-logs?{string.Join("&", queryParams)}";
        return await _apiClient.GetAsync<PagedResult<SystemLogItemModel>>(endpoint);
    }

    public async Task<ApiResponse<SystemLogItemModel>> GetLogDetailAsync(long id)
    {
        return await _apiClient.GetAsync<SystemLogItemModel>($"admin/system-logs/{id}");
    }
}
