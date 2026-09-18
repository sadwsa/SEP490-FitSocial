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
}
