using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> FindByGatewayTransactionIdAsync(string gatewayTransactionId, CancellationToken cancellationToken = default);
}
