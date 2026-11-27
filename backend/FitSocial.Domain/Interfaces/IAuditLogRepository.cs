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

    Task<(List<AuditLog> Items, int TotalCount)> GetPagedLogsAsync(
        string? search,
        string? level,
        string? source,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        string? role,
        string? actionType,
        string? status,
        string? sortOrder,
        CancellationToken cancellationToken = default);

    Task<(int TotalActivities, int AdminActivities, int StaffActivities, int FailedActivities)> GetLogsSummaryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<(List<AuditLog> Items, int TotalCount)> GetPagedAuditLogsAsync(
        string? search,
        string? action,
        string? entityType,
        Guid? actorId,
        DateTime? fromDate,
        DateTime? toDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AuditLog?> GetByIdWithActorAsync(long logId, CancellationToken cancellationToken = default);
}
