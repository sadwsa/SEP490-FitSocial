using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.AuditLogs;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditLogService> _logger;

    private static readonly Regex SensitiveKeyRegex = new(
        @"""(?i)(password|passwordhash|token|access_token|refresh_token|jwt|tokenhash|secret|secretkey|checksumkey|apikey|api_key|privatekey|connectionstring|connection_string)""\s*:\s*(""[^""]*""|[^\s,}\]]+)",
        RegexOptions.Compiled);

    public AuditLogService(
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<AuditLogService> logger)
    {
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuditLog> LogAsync(CreateAuditLogDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        return await LogAsync(
            dto.ActorAccountId,
            dto.Action,
            dto.EntityType,
            dto.EntityId,
            dto.Description,
            dto.OldValue,
            dto.NewValue,
            dto.IpAddress,
            dto.ActorRole,
            cancellationToken);
    }

    public async Task<AuditLog> LogAsync(
        Guid? actorAccountId,
        string action,
        string entityType,
        string? entityId,
        string? description = null,
        object? oldValue = null,
        object? newValue = null,
        string? ipAddress = null,
        string? actorRole = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;

            var oldValueJson = SerializeAndSanitizeJson(oldValue);
            var newValueJson = PrepareNewValueJson(newValue, description, actorRole);

            var auditLog = new AuditLog
            {
                ActorAccountId = actorAccountId,
                Action = action?.Trim(),
                EntityType = entityType?.Trim(),
                EntityId = entityId?.Trim(),
                OldValue = oldValueJson,
                NewValue = newValueJson,
                Ipaddress = ipAddress?.Trim(),
                CreatedAt = now
            };

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return auditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log for Action: {Action}, Entity: {EntityType}/{EntityId}", action, entityType, entityId);
            throw;
        }
    }

    public async Task<ApiResponseDto<PagedResultDto<AuditLogDto>>> GetAuditLogsAsync(
        GetAuditLogsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            query ??= new GetAuditLogsQueryDto();

            var (items, totalCount) = await _auditLogRepository.GetPagedAuditLogsAsync(
                query.Search,
                query.Action,
                query.EntityType,
                query.ActorId,
                query.FromDate,
                query.ToDate,
                query.PageNumber,
                query.PageSize,
                cancellationToken);

            var dtos = items.Select(MapToDto).ToList();

            var pagedResult = PagedResultDto<AuditLogDto>.Create(
                dtos,
                totalCount,
                Math.Max(1, query.PageNumber),
                Math.Clamp(query.PageSize, 1, 100));

            return ApiResponseDto<PagedResultDto<AuditLogDto>>.Ok(pagedResult, "Audit logs retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audit logs");
            return ApiResponseDto<PagedResultDto<AuditLogDto>>.Fail("Failed to load audit logs. Please try again later.");
        }
    }

    public async Task<ApiResponseDto<AuditLogDto>> GetAuditLogByIdAsync(
        long logId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = await _auditLogRepository.GetByIdWithActorAsync(logId, cancellationToken);
            if (log == null)
            {
                return ApiResponseDto<AuditLogDto>.Fail($"Audit log entry #{logId} not found.");
            }

            var dto = MapToDto(log);
            return ApiResponseDto<AuditLogDto>.Ok(dto, "Audit log retrieved successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve audit log #{LogId}", logId);
            return ApiResponseDto<AuditLogDto>.Fail("Failed to load audit log details.");
        }
    }

    private static string? PrepareNewValueJson(object? newValue, string? description, string? actorRole)
    {
        if (newValue == null && string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(actorRole))
        {
            return null;
        }

        // If newValue is already an object dictionary or model, or null
        if (newValue == null)
        {
            var dict = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(description)) dict["description"] = description;
            if (!string.IsNullOrWhiteSpace(actorRole)) dict["actorRole"] = actorRole;
            return SerializeAndSanitizeJson(dict);
        }

        if (newValue is string str)
        {
            var trimmed = str.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                // Already JSON string
                var sanitized = SanitizeSensitiveData(trimmed);
                if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(actorRole))
                {
                    return sanitized;
                }
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(sanitized);
                    if (parsed != null)
                    {
                        if (!string.IsNullOrWhiteSpace(description) && !parsed.ContainsKey("description"))
                        {
                            parsed["description"] = description;
                        }
                        if (!string.IsNullOrWhiteSpace(actorRole) && !parsed.ContainsKey("actorRole"))
                        {
                            parsed["actorRole"] = actorRole;
                        }
                        return SerializeAndSanitizeJson(parsed);
                    }
                }
                catch
                {
                    // Fallback to wrapper
                }
            }

            var fallbackDict = new Dictionary<string, object?> { ["value"] = str };
            if (!string.IsNullOrWhiteSpace(description)) fallbackDict["description"] = description;
            if (!string.IsNullOrWhiteSpace(actorRole)) fallbackDict["actorRole"] = actorRole;
            return SerializeAndSanitizeJson(fallbackDict);
        }

        try
        {
            var json = JsonSerializer.Serialize(newValue);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (parsed != null)
            {
                if (!string.IsNullOrWhiteSpace(description) && !parsed.ContainsKey("description"))
                {
                    parsed["description"] = description;
                }
                if (!string.IsNullOrWhiteSpace(actorRole) && !parsed.ContainsKey("actorRole"))
                {
                    parsed["actorRole"] = actorRole;
                }
                return SerializeAndSanitizeJson(parsed);
            }
        }
        catch
        {
            // If serialization or dictionary conversion fails, serialize directly
        }

        return SerializeAndSanitizeJson(newValue);
    }

    private static string? SerializeAndSanitizeJson(object? value)
    {
        if (value == null) return null;

        string json;
        if (value is string str)
        {
            var trimmed = str.Trim();
            if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                json = trimmed;
            }
            else
            {
                json = JsonSerializer.Serialize(new { value = str });
            }
        }
        else
        {
            json = JsonSerializer.Serialize(value);
        }

        return SanitizeSensitiveData(json);
    }

    private static string SanitizeSensitiveData(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return SensitiveKeyRegex.Replace(input, "\"$1\":\"[REDACTED]\"");
    }

    public static AuditLogDto MapToDto(AuditLog log)
    {
        var description = ExtractDescription(log.NewValue);

        return new AuditLogDto
        {
            AuditLogId = log.AuditLogId,
            ActorAccountId = log.ActorAccountId,
            ActorName = log.ActorAccount?.FullName ?? log.ActorAccount?.Email,
            ActorEmail = log.ActorAccount?.Email,
            ActorRole = log.ActorAccount?.RoleCode ?? ExtractActorRole(log.NewValue),
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Description = description,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            IpAddress = log.Ipaddress,
            CreatedAt = log.CreatedAt
        };
    }

    private static string? ExtractDescription(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                {
                    return descProp.GetString();
                }
                if (doc.RootElement.TryGetProperty("Description", out var descPropUpper) && descPropUpper.ValueKind == JsonValueKind.String)
                {
                    return descPropUpper.GetString();
                }
            }
        }
        catch
        {
            // Non-JSON or malformed
        }
        return null;
    }

    private static string? ExtractActorRole(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("actorRole", out var roleProp) && roleProp.ValueKind == JsonValueKind.String)
                {
                    return roleProp.GetString();
                }
            }
        }
        catch
        {
            // Non-JSON
        }
        return null;
    }
}
