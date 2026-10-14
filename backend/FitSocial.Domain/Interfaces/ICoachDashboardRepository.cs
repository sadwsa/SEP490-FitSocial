using System;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

/// <summary>
/// Domain repository interface for high-performance aggregate queries of Coach Dashboard metrics.
/// </summary>
public interface ICoachDashboardRepository
{
    /// <summary>
    /// Gets the count of distinct active trainees currently having a valid package with this coach.
    /// </summary>
    Task<int> GetActiveTraineesCountAsync(Guid coachId, DateTime now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of packages sold successfully for this coach (OrderStatus in PAID, COMPLETED, ACTIVE).
    /// </summary>
    Task<int> GetTotalPackagesSoldAsync(Guid coachId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes the monthly revenue obtained from packages sold within the specified month range.
    /// </summary>
    Task<decimal> GetMonthlyRevenueAsync(Guid coachId, DateTime startOfMonth, DateTime endOfMonth, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of pending requests awaiting coach action (pending orders, draft/pending plans, refund requests).
    /// </summary>
    Task<int> GetPendingRequestsCountAsync(Guid coachId, CancellationToken cancellationToken = default);

    // Thêm các method này vào trong ICoachDashboardRepository interface
    Task<(decimal TotalRevenue, int TotalOrders, double RetentionRate)> GetAnalyticsSummaryAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<List<(int Year, int Month, decimal Revenue, int TotalOrders)>> GetMonthlyStatsAsync(Guid coachId, DateTime startDate, CancellationToken cancellationToken = default);

    Task<List<(Guid TraineeId, string TraineeName, decimal TotalSpent, int TotalOrders)>> GetTopTraineesAsync(Guid coachId, int limit, CancellationToken cancellationToken = default);

}
