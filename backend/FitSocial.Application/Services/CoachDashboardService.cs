using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

/// <summary>
/// Service implementation for UC_17: View Coach Dashboard overview metrics.
/// Adheres strictly to Clean Architecture by interacting with domain repositories.
/// </summary>
public class CoachDashboardService : ICoachDashboardService
{
    private readonly ICoachDashboardRepository _dashboardRepository;
    private readonly IUserRepository _userRepository;

    public CoachDashboardService(
        ICoachDashboardRepository dashboardRepository,
        IUserRepository userRepository)
    {
        _dashboardRepository = dashboardRepository;
        _userRepository = userRepository;
    }

   


    /// <inheritdoc/>
    public async Task<ApiResponseDto<CoachDashboardMetricsDto>> GetCoachDashboardMetricsAsync(
        Guid coachId, 
        CancellationToken cancellationToken = default)
    {
        if (coachId == Guid.Empty)
        {
            throw new ValidationException("Coach ID is required.");
        }

        var coach = await _userRepository.GetByIdAsync(coachId, cancellationToken);
        if (coach == null)
        {
            throw new NotFoundException("Coach account not found.");
        }

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endOfMonth = startOfMonth.AddMonths(1);

        // Fetch aggregate metrics sequentially because DbContext is not thread-safe for concurrent operations
        var activeTrainees = await _dashboardRepository.GetActiveTraineesCountAsync(coachId, now, cancellationToken);
        var totalPackagesSold = await _dashboardRepository.GetTotalPackagesSoldAsync(coachId, cancellationToken);
        var monthlyRevenue = await _dashboardRepository.GetMonthlyRevenueAsync(coachId, startOfMonth, endOfMonth, cancellationToken);
        var pendingRequests = await _dashboardRepository.GetPendingRequestsCountAsync(coachId, cancellationToken);

        var metrics = new CoachDashboardMetricsDto
        {
            ActiveTraineesCount = activeTrainees,
            TotalPackagesSold = totalPackagesSold,
            MonthlyRevenue = monthlyRevenue,
            PendingRequestsCount = pendingRequests,
            Currency = "VND",
            LastUpdatedAt = now
        };

        return ApiResponseDto<CoachDashboardMetricsDto>.Ok(metrics, "Coach dashboard metrics retrieved successfully.");
    }
    // Thêm method này vào trong class CoachDashboardService

    public async Task<ApiResponseDto<CoachDashboardDto>> GetCoachDashboardAnalyticsAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var coach = await _userRepository.GetByIdAsync(coachId, cancellationToken);
        if (coach == null)
        {
            throw new NotFoundException("Coach account not found.");
        }

        // Gọi đồng thời các queries vào db nếu cần hoặc tuần tự
        var summary = await _dashboardRepository.GetAnalyticsSummaryAsync(coachId, cancellationToken);
        var topTrainees = await _dashboardRepository.GetTopTraineesAsync(coachId, 5, cancellationToken);

        // Cấu hình mốc thời gian 6 tháng
        var now = DateTime.UtcNow;
        var sixMonthsAgo = now.AddMonths(-5);
        var startDate = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthlyStatsRaw = await _dashboardRepository.GetMonthlyStatsAsync(coachId, startDate, cancellationToken);

        // Đảm bảo trả về đúng 6 tháng, fill dữ liệu bằng 0 cho tháng không có đơn
        var monthlyStats = new List<MonthlyStatDto>();
        for (int i = 0; i < 6; i++)
        {
            var targetMonth = startDate.AddMonths(i);
            var stat = monthlyStatsRaw.FirstOrDefault(s => s.Year == targetMonth.Year && s.Month == targetMonth.Month);

            monthlyStats.Add(new MonthlyStatDto
            {
                Month = targetMonth.ToString("MM/yyyy"),
                Revenue = stat != default ? stat.Revenue : 0,
                TotalOrders = stat != default ? stat.TotalOrders : 0
            });
        }

        var result = new CoachDashboardDto
        {
            TotalRevenue = summary.TotalRevenue,
            TotalOrders = summary.TotalOrders,
            RetentionRate = summary.RetentionRate,
            MonthlyStats = monthlyStats,
            TopTrainees = topTrainees.Select(t => new TopTraineeDto
            {
                TraineeId = t.TraineeId,
                TraineeName = t.TraineeName,
                TotalSpent = t.TotalSpent,
                TotalOrders = t.TotalOrders
            }).ToList()
        };

        return ApiResponseDto<CoachDashboardDto>.Ok(result, "Analytics retrieved successfully.");
    }

}
