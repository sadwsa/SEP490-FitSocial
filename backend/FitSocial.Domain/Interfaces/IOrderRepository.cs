using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> ListPendingActivationByUserAsync(Guid userId, string orderType, CancellationToken cancellationToken = default);
    Task<List<Order>> GetOrdersByBuyerIdAsync(Guid buyerId, CancellationToken cancellationToken = default);
    Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<bool> HasSoldTrainingPackageAsync(Guid coachId, CancellationToken cancellationToken = default);
}

