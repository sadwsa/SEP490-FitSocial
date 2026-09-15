using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> ListPendingActivationByUserAsync(Guid userId, string orderType, CancellationToken cancellationToken = default);
}
