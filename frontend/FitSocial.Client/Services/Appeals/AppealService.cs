using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Appeals;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Appeals;

public class AppealService : IAppealService
{
    private readonly ApiClient _apiClient;

    public AppealService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<List<AppealableReport>>> GetMyAppealableReportsAsync(string? appealToken = null)
    {
        try
        {
            var endpoint = "appeals/my-reports";
            if (!string.IsNullOrWhiteSpace(appealToken))
            {
                endpoint += $"?appealToken={Uri.EscapeDataString(appealToken.Trim())}";
            }

            var result = await _apiClient.GetAsync<List<AppealableReport>>(endpoint);
            if (result != null && result.Success && result.Data == null)
            {
                result.Data = new List<AppealableReport>();
            }

            return result ?? new ApiResponse<List<AppealableReport>>
            {
                Success = false,
                Message = "Unable to load reports. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<AppealableReport>>
            {
                Success = false,
                Message = $"Error loading reports: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AppealableReport>> GetAppealableReportByIdAsync(Guid reportId, string? appealToken = null)
    {
        try
        {
            var endpoint = $"appeals/reports/{reportId}";
            if (!string.IsNullOrWhiteSpace(appealToken))
            {
                endpoint += $"?appealToken={Uri.EscapeDataString(appealToken.Trim())}";
            }

            var result = await _apiClient.GetAsync<AppealableReport>(endpoint);
            return result ?? new ApiResponse<AppealableReport>
            {
                Success = false,
                Message = "Unable to load report details."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AppealableReport>
            {
                Success = false,
                Message = $"Error loading report details: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AppealDetail>> SubmitAppealAsync(SubmitAppealRequest request)
    {
        try
        {
            var result = await _apiClient.PostAsync<SubmitAppealRequest, AppealDetail>("appeals", request);
            return result ?? new ApiResponse<AppealDetail>
            {
                Success = false,
                Message = "Failed to submit appeal. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AppealDetail>
            {
                Success = false,
                Message = $"Error submitting appeal: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AppealDetail>> GetAppealByIdAsync(Guid appealId, string? appealToken = null)
    {
        try
        {
            var endpoint = $"appeals/{appealId}";
            if (!string.IsNullOrWhiteSpace(appealToken))
            {
                endpoint += $"?appealToken={Uri.EscapeDataString(appealToken.Trim())}";
            }

            var result = await _apiClient.GetAsync<AppealDetail>(endpoint);
            return result ?? new ApiResponse<AppealDetail>
            {
                Success = false,
                Message = "Unable to load appeal details."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AppealDetail>
            {
                Success = false,
                Message = $"Error loading appeal details: {ex.Message}"
            };
        }
    }
}
