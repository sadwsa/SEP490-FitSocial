using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPayoutRepository : IRepository<Payout>
{
    Task<Payout?> GetByIdWithDetailsAsync(Guid payoutId, CancellationToken cancellationToken = default);
    Task<List<Payout>> ListHistoryAsync(int? month, int? year, string? status, Guid? coachId, CancellationToken cancellationToken = default);
    Task<Payout?> FindCycleAsync(Guid coachId, int month, int year, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completed package-sale orders of a coach created inside [from, to).
    /// </summary>
    Task<List<Order>> ListCompletedOrdersForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approved refunds of a coach's orders refunded inside [from, to).
    /// </summary>
    Task<List<RefundRequest>> ListApprovedRefundsForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Distinct coach ids having completed package sales inside [from, to).
    /// </summary>
    Task<List<Guid>> ListCoachesWithSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    void RemoveItems(IEnumerable<PayoutItem> items);
}
