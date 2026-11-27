using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPayoutRepository : IRepository<Payout>
{
    Task<Payout?> GetByIdWithDetailsAsync(Guid payoutId, CancellationToken cancellationToken = default);

    Task<List<Payout>> ListHistoryAsync(int? month, int? year, string? status, Guid? coachId, CancellationToken cancellationToken = default);

    Task<Payout?> FindCycleAsync(Guid coachId, int month, int year, CancellationToken cancellationToken = default);

    Task<List<Order>> ListCompletedOrdersForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<List<RefundRequest>> ListApprovedRefundsForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<List<Guid>> ListCoachesWithSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    void RemoveItems(IEnumerable<PayoutItem> items);
}
