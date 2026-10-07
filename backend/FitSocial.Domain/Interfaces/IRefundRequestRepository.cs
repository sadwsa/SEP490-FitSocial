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

    Task<(List<RefundRequest> Items, int TotalCount)> GetPagedRefundRequestsAsync(
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<RefundRequest?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasPendingRefundAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<bool> HasApprovedRefundAsync(Guid orderId, CancellationToken cancellationToken = default);
}
