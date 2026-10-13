using System;
using System.Text;
using System.Threading.Tasks;
using FitSocial.Client.Models.CoachCertificate;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.CoachCertificate;

public class CoachCertificateService : ICoachCertificateService
{
    private readonly ApiClient _apiClient;
    private const string BaseEndpoint = "admin/coach-certificates";

    public CoachCertificateService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<CoachCertificateListResponseModel>> GetCertificatesAsync(GetCoachCertificatesQueryModel query)
    {
        var sb = new StringBuilder($"{BaseEndpoint}?");
        sb.Append($"pageNumber={query.PageNumber}&pageSize={query.PageSize}");

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            sb.Append($"&status={Uri.EscapeDataString(query.Status)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            sb.Append($"&search={Uri.EscapeDataString(query.Search.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy))
        {
            sb.Append($"&sortBy={Uri.EscapeDataString(query.SortBy)}");
        }

        return await _apiClient.GetAsync<CoachCertificateListResponseModel>(sb.ToString());
    }

    public async Task<ApiResponse<CoachCertificateDetailModel>> GetCertificateByIdAsync(Guid certificateId)
    {
        return await _apiClient.GetAsync<CoachCertificateDetailModel>($"{BaseEndpoint}/{certificateId}");
    }

    public async Task<ApiResponse<CoachCertificateDetailModel>> ApproveCertificateAsync(Guid certificateId)
    {
        return await _apiClient.PutAsync<CoachCertificateDetailModel>($"{BaseEndpoint}/{certificateId}/approve");
    }

    public async Task<ApiResponse<CoachCertificateDetailModel>> RejectCertificateAsync(Guid certificateId, RejectCoachCertificateModel request)
    {
        return await _apiClient.PutAsync<RejectCoachCertificateModel, CoachCertificateDetailModel>(
            $"{BaseEndpoint}/{certificateId}/reject", request);
    }
}
