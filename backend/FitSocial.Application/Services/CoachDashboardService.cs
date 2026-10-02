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
}
