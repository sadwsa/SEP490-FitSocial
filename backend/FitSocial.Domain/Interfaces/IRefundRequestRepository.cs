using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface IRefundRequestRepository : IRepository<RefundRequest>
{
    Task<List<RefundRequest>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<RefundRequest>> ListAllWithDetailsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<RefundRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasPendingRefundAsync(Guid orderId, CancellationToken cancellationToken = default);
}
