using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Reports;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Reports;

public class ReportService : IReportService
{
    private readonly ApiClient _apiClient;

    public ReportService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<ReportListItemDto>>> GetReportsAsync(
        string? searchTerm = null,
        string? status = null,
        string? targetType = null,
        Guid? reporterId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            var query = new List<string>();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query.Add($"status={Uri.EscapeDataString(status.Trim().ToUpperInvariant())}");
            }

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                query.Add($"targetType={Uri.EscapeDataString(targetType.Trim().ToUpperInvariant())}");
            }

            if (reporterId.HasValue && reporterId.Value != Guid.Empty)
            {
                query.Add($"reporterId={reporterId.Value}");
            }

            if (fromDate.HasValue)
            {
                query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
            }

            if (toDate.HasValue)
            {
                query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
            }

            query.Add($"pageNumber={Math.Max(1, pageNumber)}");
            query.Add($"pageSize={Math.Max(1, pageSize)}");

            var endpoint = "reports";
            if (query.Count > 0)
            {
                endpoint += "?" + string.Join("&", query);
            }

            var result = await _apiClient.GetAsync<PagedResult<ReportListItemDto>>(endpoint);
            if (result != null && result.Success && result.Data == null)
            {
                result.Data = new PagedResult<ReportListItemDto>();
            }

            return result ?? new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = "Unable to load reports. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = $"Error loading reports: {ex.Message}"
            };
        }
    }
}
