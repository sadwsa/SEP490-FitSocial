using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemLogs;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class SystemLogService : ISystemLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<SystemLogService> _logger;

    private static readonly Regex SensitiveKeyRegex = new(
        @"""(?i)(password|passwordhash|token|access_token|refresh_token|jwt|tokenhash|secret|secretkey|checksumkey|apikey|api_key|privatekey|connectionstring|connection_string)""\s*:\s*(""[^""]*""|[^\s,}\]]+)",
        RegexOptions.Compiled);

    private static readonly Regex ConnectionStringPassRegex = new(
        @"(?i)(password|pwd)\s*=\s*[^;]+",
        RegexOptions.Compiled);

    private static readonly Regex BearerTokenRegex = new(
        @"(?i)bearer\s+[a-zA-Z0-9_\-\.]+",
        RegexOptions.Compiled);

    public SystemLogService(
        IAuditLogRepository auditLogRepository,
        ILogger<SystemLogService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _logger = logger;
    }

    public async Task<ApiResponseDto<PagedResultDto<SystemLogItemDto>>> GetSystemLogsAsync(
        GetSystemLogsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            query ??= new GetSystemLogsQueryDto();

            var (items, totalCount) = await _auditLogRepository.GetPagedLogsAsync(
                query.Search,
                query.Level,
                query.Source,
                query.FromDate,
                query.ToDate,
                query.PageNumber,
                query.PageSize,
                cancellationToken);

            var dtos = items.Select(MapToDto).ToList();

            var pagedResult = PagedResultDto<SystemLogItemDto>.Create(
                dtos,
                totalCount,
                Math.Max(1, query.PageNumber),
                Math.Clamp(query.PageSize, 1, 100));

            return ApiResponseDto<PagedResultDto<SystemLogItemDto>>.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system logs");
            return ApiResponseDto<PagedResultDto<SystemLogItemDto>>.Fail("Failed to load system logs. Please try again later.");
        }
    }

    public async Task<ApiResponseDto<SystemLogItemDto>> GetLogDetailAsync(
        long logId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = await _auditLogRepository.GetByIdWithActorAsync(logId, cancellationToken);
            if (log == null)
            {
                return ApiResponseDto<SystemLogItemDto>.Fail($"System log entry #{logId} not found.");
            }

            var dto = MapToDto(log);
            return ApiResponseDto<SystemLogItemDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system log detail for #{LogId}", logId);
            return ApiResponseDto<SystemLogItemDto>.Fail("Failed to load system log details.");
        }
    }

    public static SystemLogItemDto MapToDto(AuditLog log)
    {
        return new SystemLogItemDto
        {
            LogId = log.AuditLogId,
            LogIdDisplay = $"LOG-{log.AuditLogId:D6}",
            Timestamp = log.CreatedAt ?? DateTime.UtcNow,
            Level = DetermineLogLevel(log.Action),
            Source = string.IsNullOrWhiteSpace(log.EntityType) ? "System" : log.EntityType.Trim(),
            Action = string.IsNullOrWhiteSpace(log.Action) ? "EVENT" : log.Action.Trim(),
            Message = GenerateReadableMessage(log),
            ActorId = log.ActorAccountId,
            ActorName = log.ActorAccount?.FullName ?? (log.ActorAccountId.HasValue ? $"User {log.ActorAccountId.Value.ToString()[..8]}" : "System"),
            ActorEmail = log.ActorAccount?.Email,
            IpAddress = log.Ipaddress,
            EntityId = log.EntityId,
            OldValue = Sanitize(log.OldValue),
            NewValue = Sanitize(log.NewValue),
            Details = GenerateDetailsSummary(log)
        };
    }

    public static string DetermineLogLevel(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "Information";
        var a = action.ToUpperInvariant();

        if (a.Contains("ERROR") || a.Contains("EXCEPTION") || a.Contains("FAIL") || a.Contains("CRITICAL"))
            return "Error";

        if (a.Contains("REJECT") || a.Contains("WARN") || a.Contains("LOCK") ||
            a.Contains("SUSPEND") || a.Contains("CANCEL") || a.Contains("REVOKE") || a.Contains("DELETE"))
            return "Warning";

        return "Information";
    }

    public static string GenerateReadableMessage(AuditLog log)
    {
        var action = log.Action?.Trim() ?? string.Empty;
        var entityType = log.EntityType?.Trim() ?? "Entity";
        var entityId = log.EntityId?.Trim() ?? string.Empty;
        var actor = log.ActorAccount?.FullName ?? (log.ActorAccountId.HasValue ? "User" : "System");

        if (action.Equals("UPDATE_PRICE", StringComparison.OrdinalIgnoreCase))
        {
            return $"Updated subscription plan price for {entityType} ({entityId}) by {actor}";
        }

        if (action.Equals("CREATE_REFUND_REQUEST", StringComparison.OrdinalIgnoreCase))
        {
            return $"Submitted new refund request for Order {entityId} by {actor}";
        }

        if (action.Equals("APPROVE_REFUND_REQUEST", StringComparison.OrdinalIgnoreCase))
        {
            return $"Approved refund request {entityId} by {actor}";
        }

        if (action.Equals("REJECT_REFUND_REQUEST", StringComparison.OrdinalIgnoreCase))
        {
            return $"Rejected refund request {entityId} by {actor}";
        }

        if (action.Equals("PROCESS_PAYOUT", StringComparison.OrdinalIgnoreCase))
        {
            return $"Processed coach payout {entityId} by {actor}";
        }

        if (action.Equals("SYSTEM_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return $"Unhandled runtime system error occurred on path: {entityId}";
        }

        if (action.Equals("BUSINESS_WARNING", StringComparison.OrdinalIgnoreCase))
        {
            return $"Business validation event on path: {entityId}";
        }

        // Default friendly format
        var formattedAction = action.Replace('_', ' ');
        if (!string.IsNullOrEmpty(entityId))
        {
            return $"{formattedAction} on {entityType} [{entityId}] by {actor}";
        }

        return $"{formattedAction} on {entityType} by {actor}";
    }

    public static string GenerateDetailsSummary(AuditLog log)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(log.Action)) parts.Add($"Action: {log.Action}");
        if (!string.IsNullOrWhiteSpace(log.EntityType)) parts.Add($"Source: {log.EntityType}");
        if (!string.IsNullOrWhiteSpace(log.EntityId)) parts.Add($"Target: {log.EntityId}");
        if (!string.IsNullOrWhiteSpace(log.Ipaddress)) parts.Add($"IP: {log.Ipaddress}");
        if (log.ActorAccount != null) parts.Add($"Actor: {log.ActorAccount.FullName} ({log.ActorAccount.Email})");
        return string.Join(" | ", parts);
    }

    public static string? Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        // Mask JSON sensitive keys
        var sanitized = SensitiveKeyRegex.Replace(input, match =>
        {
            var keyGroup = match.Groups[1].Value;
            return $"\"{keyGroup}\": \"***REDACTED***\"";
        });

        // Mask connection string passwords
        sanitized = ConnectionStringPassRegex.Replace(sanitized, "$1=***REDACTED***");

        // Mask bearer tokens
        sanitized = BearerTokenRegex.Replace(sanitized, "Bearer ***REDACTED***");

        return sanitized;
    }
}
