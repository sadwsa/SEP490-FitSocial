using System.Threading.Tasks;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Coaches;

/// <summary>
/// Client service interface for UC_17 Coach Dashboard overview.
/// </summary>
public interface ICoachDashboardService
{
    Task<ApiResponse<CoachDashboardMetricsDto>> GetOverviewMetricsAsync();
    // Thêm method này bên dưới GetOverviewMetricsAsync
    Task<ApiResponse<CoachDashboardDto>> GetAnalyticsAsync(Guid coachId);

}
