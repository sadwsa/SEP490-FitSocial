using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachUpgradeRepository : IRepository<CoachUpgrade>
{
    Task<Dictionary<Guid, (int Total, int Active)>> GetCountsByPlanIdsAsync(IEnumerable<Guid> planIds, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
    Task<int> CountByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<int> CountActiveByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<CoachUpgrade?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    // Backward compat (deprecated, use PlanId variants)
    Task<Dictionary<Guid, (int Total, int Active)>> GetCountsByPriceIdsAsync(IEnumerable<Guid> priceIds, CancellationToken cancellationToken = default);
    Task<int> CountByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default);
    Task<int> CountActiveByPriceIdAsync(Guid priceId, CancellationToken cancellationToken = default);
}
