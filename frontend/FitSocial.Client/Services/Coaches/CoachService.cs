using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Services.Http;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Coaches;

public class CoachService : ICoachService
{
    private readonly ApiClient _apiClient;
    private const string BaseEndpoint = "coaches";

    public CoachService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<IEnumerable<CoachListDto>>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchKeyword))
            queryParts.Add($"searchKeyword={Uri.EscapeDataString(searchKeyword)}");

        if (minExperience.HasValue && minExperience.Value > 0)
            queryParts.Add($"minExperience={minExperience.Value}");

        if (!string.IsNullOrWhiteSpace(sortBy))
            queryParts.Add($"sortBy={Uri.EscapeDataString(sortBy)}");

        var queryString = queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : "";
        return await _apiClient.GetAsync<IEnumerable<CoachListDto>>($"{BaseEndpoint}{queryString}");
    }

    public async Task<ApiResponse<List<TopCoachDto>>> GetTopCoachesAsync(int count = 5)
    {
        return await _apiClient.GetAsync<List<TopCoachDto>>($"{BaseEndpoint}/top?count={count}");
    }
    public async Task<FitSocial.Client.Models.Common.ApiResponse<bool>> SubmitReviewAsync(Guid coachId, FitSocial.Client.Models.Coaches.CreateReviewDto dto)
    {
        return await _apiClient.PostAsync<FitSocial.Client.Models.Coaches.CreateReviewDto, bool>($"{BaseEndpoint}/{coachId}/reviews", dto);
    }
    public async Task<FitSocial.Client.Models.Common.ApiResponse<bool>> UpdateReviewAsync(Guid reviewId, FitSocial.Client.Models.Coaches.UpdateReviewDto dto)
    {
        return await _apiClient.PutAsync<FitSocial.Client.Models.Coaches.UpdateReviewDto, bool>($"{BaseEndpoint}/reviews/{reviewId}", dto);
    }

    public async Task<FitSocial.Client.Models.Common.ApiResponse<bool>> DeleteReviewAsync(Guid reviewId)
    {
        return await _apiClient.DeleteAsync<bool>($"{BaseEndpoint}/reviews/{reviewId}");
    }

    public async Task<ApiResponse<CoachApplicationListResponseDto>> GetCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            queryParts.Add($"search={Uri.EscapeDataString(search.Trim())}");

        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status.Trim())}");

        if (minExperience.HasValue)
            queryParts.Add($"minExperience={minExperience.Value}");

        if (maxExperience.HasValue)
            queryParts.Add($"maxExperience={maxExperience.Value}");

        if (!string.IsNullOrWhiteSpace(sortBy))
            queryParts.Add($"sortBy={Uri.EscapeDataString(sortBy.Trim())}");

        queryParts.Add($"pageNumber={pageNumber}");
        queryParts.Add($"pageSize={pageSize}");

        var queryString = "?" + string.Join("&", queryParts);
        return await _apiClient.GetAsync<CoachApplicationListResponseDto>($"{BaseEndpoint}/applications{queryString}");
    }

    public async Task<ApiResponse<CoachApplicationDetailDto>> GetCoachApplicationDetailsAsync(Guid coachId)
    {
        return await _apiClient.GetAsync<CoachApplicationDetailDto>($"{BaseEndpoint}/applications/{coachId}");
    }

    public async Task<ApiResponse<CoachApplicationStatusCountsDto>> GetCoachApplicationStatusCountsAsync()
    {
        return await _apiClient.GetAsync<CoachApplicationStatusCountsDto>($"{BaseEndpoint}/applications/status-counts");
    }
}
