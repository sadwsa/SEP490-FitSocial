using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Refunds;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Refunds;

public interface IRefundService
{
    Task<ApiResponse<PagedResult<RefundRequestItem>>> GetPagedRefundRequestsAsync(GetRefundRequestsQuery query);
    Task<ApiResponse<RefundRequestItem>> GetRefundRequestByIdAsync(Guid id);
    Task<ApiResponse<RefundRequestItem>> ApproveRefundRequestAsync(Guid id, ApproveRefundRequest request);
    Task<ApiResponse<RefundRequestItem>> RejectRefundRequestAsync(Guid id, RejectRefundRequest request);
}

public class RefundApiService : IRefundService
{
    private readonly ApiClient _apiClient;

    public RefundApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<PagedResult<RefundRequestItem>>> GetPagedRefundRequestsAsync(GetRefundRequestsQuery query)
    {
        var queryParams = new List<string>
        {
            $"pageNumber={query.PageNumber}",
            $"pageSize={query.PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            queryParams.Add($"status={Uri.EscapeDataString(query.Status.Trim().ToUpperInvariant())}");
        }

        if (query.FromDate.HasValue)
        {
            queryParams.Add($"fromDate={Uri.EscapeDataString(query.FromDate.Value.ToString("O"))}");
        }

        if (query.ToDate.HasValue)
        {
            queryParams.Add($"toDate={Uri.EscapeDataString(query.ToDate.Value.ToString("O"))}");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            queryParams.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
        }

        var endpoint = "RefundRequests?" + string.Join("&", queryParams);
        return _apiClient.GetAsync<PagedResult<RefundRequestItem>>(endpoint);
    }

    public Task<ApiResponse<RefundRequestItem>> GetRefundRequestByIdAsync(Guid id)
    {
        return _apiClient.GetAsync<RefundRequestItem>($"RefundRequests/{id}");
    }

    public Task<ApiResponse<RefundRequestItem>> ApproveRefundRequestAsync(Guid id, ApproveRefundRequest request)
    {
        return _apiClient.PostAsync<ApproveRefundRequest, RefundRequestItem>($"RefundRequests/{id}/approve", request);
    }

    public Task<ApiResponse<RefundRequestItem>> RejectRefundRequestAsync(Guid id, RejectRefundRequest request)
    {
        return _apiClient.PostAsync<RejectRefundRequest, RefundRequestItem>($"RefundRequests/{id}/reject", request);
    }
}
