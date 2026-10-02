using System.Threading.Tasks;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Coaches;

/// <summary>
/// Client service implementation calling backend UC_17 endpoint: GET /api/coach/dashboard/overview
/// </summary>
public class CoachDashboardService : ICoachDashboardService
{
    private readonly ApiClient _apiClient;
    private const string OverviewEndpoint = "coach/dashboard/overview";

    public CoachDashboardService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<CoachDashboardMetricsDto>> GetOverviewMetricsAsync()
    {
        return await _apiClient.GetAsync<CoachDashboardMetricsDto>(OverviewEndpoint);
    }
}
