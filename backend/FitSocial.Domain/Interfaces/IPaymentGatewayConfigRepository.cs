using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPaymentGatewayConfigRepository : IRepository<PaymentGatewayConfig>
{
    Task<PaymentGatewayConfig?> GetActiveByNameAsync(string gatewayName, CancellationToken cancellationToken = default);
    Task<List<PaymentGatewayConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default);
}
