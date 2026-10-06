using System.Threading.Tasks;
using FitSocial.Client.Models.Analytics;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Analytics;

public interface IRevenueAnalyticsService
{
    Task<ApiResponse<FullRevenueAnalyticsResponseModel>> GetRevenueOverviewAsync(RevenueAnalyticsFilterModel filter);
    Task<bool> ExportRevenueCsvAsync(RevenueAnalyticsFilterModel filter);
}
