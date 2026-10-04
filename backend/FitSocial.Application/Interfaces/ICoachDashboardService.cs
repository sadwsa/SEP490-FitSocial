using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

/// <summary>
/// Service interface for UC_17: View Coach Dashboard metrics and overview.
/// </summary>
public interface ICoachDashboardService
{
    /// <summary>
    /// Computes and returns the 4 primary performance metrics for a given coach.
    /// </summary>
    /// <param name="coachId">The authenticated coach user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Standard API response containing <see cref="CoachDashboardMetricsDto"/>.</returns>
    Task<ApiResponseDto<CoachDashboardMetricsDto>> GetCoachDashboardMetricsAsync(
        Guid coachId, 
        CancellationToken cancellationToken = default);
}
