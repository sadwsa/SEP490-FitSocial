using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Analytics;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Services;

public interface IRevenueAnalyticsService
{
    Task<ApiResponseDto<FullRevenueAnalyticsResponseDto>> GetRevenueAnalyticsAsync(RevenueAnalyticsFilterDto filter, CancellationToken ct = default);
    Task<byte[]> ExportRevenueCsvAsync(RevenueAnalyticsFilterDto filter, CancellationToken ct = default);
}
