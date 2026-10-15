using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.AuditLogs;
using FitSocial.Application.DTOs.Common;
using FitSocial.Domain.Entities;

namespace FitSocial.Application.Interfaces;

public interface IAuditLogService
{
    Task<AuditLog> LogAsync(CreateAuditLogDto dto, CancellationToken cancellationToken = default);

    Task<AuditLog> LogAsync(
        Guid? actorAccountId,
        string action,
        string entityType,
        string? entityId,
        string? description = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<PagedResultDto<AuditLogDto>>> GetAuditLogsAsync(
        GetAuditLogsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AuditLogDto>> GetAuditLogByIdAsync(
        long logId,
        CancellationToken cancellationToken = default);
}
