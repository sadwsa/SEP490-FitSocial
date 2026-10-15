using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<(List<AuditLog> Items, int TotalCount)> GetPagedLogsAsync(
        string? search,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AuditLog?> GetByIdWithActorAsync(long logId, CancellationToken cancellationToken = default);
}
