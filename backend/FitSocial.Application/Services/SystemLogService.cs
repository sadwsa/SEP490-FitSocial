using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemLogs;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace FitSocial.Application.Services;

public class SystemLogService : ISystemLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<SystemLogService> _logger;

    private static readonly Regex SensitiveKeyRegex = new(
        @"""(?i)(password|passwordhash|newpassword|oldpassword|currentpassword|confirmpassword|token|access_token|refresh_token|jwt|tokenhash|secret|secretkey|checksumkey|apikey|api_key|privatekey|connectionstring|connection_string|stacktrace|stack_trace)""\s*:\s*(""[^""]*""|[^\s,}\]]+)",
        RegexOptions.Compiled);

    private static readonly Regex ConnectionStringPassRegex = new(
        @"(?i)(password|pwd)\s*=\s*[^;]+",
        RegexOptions.Compiled);

    private static readonly Regex BearerTokenRegex = new(
        @"(?i)bearer\s+[a-zA-Z0-9_\-\.]+",
        RegexOptions.Compiled);

    private static readonly string[] DisplayNameCandidateKeys =
    {
        "targetName", "fullName", "locationName", "planName", "planDescription", "gatewayName",
        "certificateName", "coachName", "package", "order", "buyer", "seller",
        "title", "name", "targetEmail", "email", "code"
    };

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

            var hasExtendedFilters =
                !string.IsNullOrWhiteSpace(query.Role) ||
                !string.IsNullOrWhiteSpace(query.ActionType) ||
                !string.IsNullOrWhiteSpace(query.Status) ||
                !string.IsNullOrWhiteSpace(query.SortOrder);

            (List<AuditLog> items, int totalCount) repoResult;

            if (hasExtendedFilters)
            {
                repoResult = await _auditLogRepository.GetPagedLogsAsync(
                    query.Search,
                    query.Level,
                    query.Source,
                    query.FromDate,
                    query.ToDate,
                    query.PageNumber,
                    query.PageSize,
                    query.Role,
                    query.ActionType,
                    query.Status,
                    query.SortOrder,
                    cancellationToken);
            }
            else
            {
                repoResult = await _auditLogRepository.GetPagedLogsAsync(
                    query.Search,
                    query.Level,
                    query.Source,
                    query.FromDate,
                    query.ToDate,
                    query.PageNumber,
                    query.PageSize,
                    cancellationToken);
            }

            // Enforce ADMIN / STAFF actor filter as defense-in-depth if RoleCode is explicitly populated
            var filteredItems = repoResult.items
                .Where(IsAllowedAdministrativeLog)
                .ToList();

            var removedCount = repoResult.items.Count - filteredItems.Count;
            var adjustedTotalCount = Math.Max(0, repoResult.totalCount - removedCount);

            var dtos = filteredItems.Select(MapToDto).ToList();

            var pagedResult = PagedResultDto<SystemLogItemDto>.Create(
                dtos,
                adjustedTotalCount,
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

    public async Task<ApiResponseDto<SystemLogSummaryDto>> GetSystemLogsSummaryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (total, admin, staff, failed) = await _auditLogRepository.GetLogsSummaryAsync(
                fromDate,
                toDate,
                cancellationToken);

            var summary = new SystemLogSummaryDto
            {
                TotalActivities = total,
                AdminActivities = admin,
                StaffActivities = staff,
                FailedActivities = failed
            };

            return ApiResponseDto<SystemLogSummaryDto>.Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve system logs summary");
            return ApiResponseDto<SystemLogSummaryDto>.Fail("Failed to load system log summary statistics.");
        }
    }

    public async Task<ApiResponseDto<SystemLogItemDto>> GetLogDetailAsync(
        long logId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = await _auditLogRepository.GetByIdWithActorAsync(logId, cancellationToken);
            if (log == null || !IsAllowedAdministrativeLog(log))
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

    public static bool IsAllowedAdministrativeLog(AuditLog log)
    {
        if (log.ActorAccount == null)
        {
            return true;
        }

        var roleCode = log.ActorAccount.RoleCode?.Trim();
        if (string.IsNullOrEmpty(roleCode))
        {
            return true;
        }

        return roleCode.Equals(RoleConstants.Admin, StringComparison.OrdinalIgnoreCase) ||
               roleCode.Equals(RoleConstants.Staff, StringComparison.OrdinalIgnoreCase);
    }

    public static SystemLogItemDto MapToDto(AuditLog log)
    {
        var rawRole = log.ActorAccount?.RoleCode?.Trim();
        var normalizedRole = !string.IsNullOrWhiteSpace(rawRole)
            ? rawRole.ToUpperInvariant()
            : (log.ActorAccount?.IsInternal == true ? RoleConstants.Staff : null);

        var targetDisplayName = ExtractTargetDisplayName(log);

        return new SystemLogItemDto
        {
            LogId = log.AuditLogId,
            LogIdDisplay = $"LOG-{log.AuditLogId:D6}",
            Timestamp = log.CreatedAt ?? DateTime.UtcNow,
            Level = DetermineLogLevel(log.Action),
            ActionType = DetermineActionType(log.Action),
            Status = DetermineStatus(log),
            Source = string.IsNullOrWhiteSpace(log.EntityType) ? "System" : log.EntityType.Trim(),
            Action = string.IsNullOrWhiteSpace(log.Action) ? "EVENT" : log.Action.Trim(),
            Message = GenerateReadableMessage(log, targetDisplayName),
            ActorId = log.ActorAccountId,
            ActorName = log.ActorAccount?.FullName ?? (log.ActorAccountId.HasValue ? $"User {log.ActorAccountId.Value.ToString()[..8]}" : "System"),
            ActorEmail = log.ActorAccount?.Email,
            ActorRole = normalizedRole,
            IpAddress = log.Ipaddress,
            EntityId = log.EntityId,
            TargetDisplayName = targetDisplayName,
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

    public static string DetermineActionType(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "UPDATE";
        var a = action.Trim().ToUpperInvariant();

        if (a.Contains("CREATE") || a.Contains("ADD")) return "CREATE";
        if (a.Contains("DELETE") || a.Contains("REMOVE")) return "DELETE";
        if (a.Contains("UNLOCK")) return "UNLOCK";
        if (a.Contains("LOCK")) return "LOCK";
        if (a.Contains("APPROVE")) return "APPROVE";
        if (a.Contains("REJECT") || a.Contains("DISMISS")) return "REJECT";
        if (a.Contains("UPDATE") || a.Contains("EDIT") || a.Contains("TOGGLE") ||
            a.Contains("SET_DEFAULT") || a.Contains("CHANGE_") || a.Contains("PROCESS_") ||
            a.Contains("RESOLVE"))
            return "UPDATE";

        return "UPDATE";
    }

    public static string DetermineStatus(AuditLog log)
    {
        var action = log.Action?.ToUpperInvariant() ?? string.Empty;
        if (action.Contains("ERROR") || action.Contains("EXCEPTION") ||
            action.Contains("FAIL") || action.Contains("CRITICAL"))
        {
            return "Failed";
        }

        var newVal = log.NewValue?.ToLowerInvariant() ?? string.Empty;
        if (newVal.Contains("\"status\":\"failed\"") ||
            newVal.Contains("\"status\": \"failed\"") ||
            newVal.Contains("\"success\":false") ||
            newVal.Contains("\"success\": false"))
        {
            return "Failed";
        }

        return "Success";
    }

    public static string? ExtractTargetDisplayName(AuditLog log)
    {
        var fromNew = TryExtractDisplayNameFromJson(log.NewValue, log.EntityType);
        if (!string.IsNullOrWhiteSpace(fromNew))
        {
            return fromNew;
        }

        var fromOld = TryExtractDisplayNameFromJson(log.OldValue, log.EntityType);
        if (!string.IsNullOrWhiteSpace(fromOld))
        {
            return fromOld;
        }

        if (string.Equals(log.Action, "UPDATE_PROFILE", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(log.ActorAccount?.FullName))
        {
            return log.ActorAccount.FullName;
        }

        return null;
    }

    private static string? TryExtractDisplayNameFromJson(string? json, string? entityType = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        props[prop.Name] = val.Trim();
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    props[prop.Name] = prop.Value.ToString();
                }
            }

            // 1. RefundRequest: take the requester/buyer name directly (e.g. "Nguyễn Văn Sang") without order code
            if (props.TryGetValue("buyer", out var buyer) && !string.IsNullOrWhiteSpace(buyer))
            {
                return buyer;
            }

            if (props.TryGetValue("targetName", out var explicitTarget) && !string.IsNullOrWhiteSpace(explicitTarget))
            {
                return explicitTarget;
            }

            // 2. Certificate / Payout: take coachName or certificateName without extra codes
            if (props.TryGetValue("coachName", out var cName) && !string.IsNullOrWhiteSpace(cName))
            {
                return cName;
            }

            if (props.TryGetValue("certificateName", out var certName) && !string.IsNullOrWhiteSpace(certName))
            {
                return certName;
            }

            // 3. Subscription plan description
            if (string.Equals(entityType, "CoachSubscriptionPlan", StringComparison.OrdinalIgnoreCase))
            {
                if (props.TryGetValue("planDescription", out var planDesc) && !string.IsNullOrWhiteSpace(planDesc))
                    return planDesc;
                if (props.TryGetValue("description", out var desc) && !string.IsNullOrWhiteSpace(desc) && !desc.StartsWith("Updated ", StringComparison.OrdinalIgnoreCase) && !desc.StartsWith("Created ", StringComparison.OrdinalIgnoreCase) && !desc.StartsWith("Deleted ", StringComparison.OrdinalIgnoreCase) && !desc.StartsWith("Deactivated ", StringComparison.OrdinalIgnoreCase))
                    return desc;
            }

            foreach (var candidate in DisplayNameCandidateKeys)
            {
                if (props.TryGetValue(candidate, out var val) && !string.IsNullOrWhiteSpace(val) && !val.StartsWith("#ORD-", StringComparison.OrdinalIgnoreCase))
                {
                    return val;
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }

        return null;
    }

    public static string GenerateReadableMessage(AuditLog log, string? targetDisplayName = null)
    {
        var action = log.Action?.Trim() ?? string.Empty;
        var entityType = log.EntityType?.Trim() ?? "Entity";
        var entityId = log.EntityId?.Trim() ?? string.Empty;
        var actor = log.ActorAccount?.FullName ?? (log.ActorAccountId.HasValue ? "Administrator" : "System");

        var isGuidEntityId = Guid.TryParse(entityId, out _);
        var friendlyEntityType = FormatFriendlyEntityType(entityType);

        var targetLabel = !string.IsNullOrWhiteSpace(targetDisplayName)
            ? targetDisplayName
            : (!string.IsNullOrWhiteSpace(entityId) && !isGuidEntityId ? entityId : friendlyEntityType);

        if (action.Equals("SYSTEM_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(entityId) && !isGuidEntityId
                ? $"Unhandled runtime system error occurred on path: {entityId}"
                : "Unhandled runtime system error occurred";
        }

        if (action.Equals("BUSINESS_WARNING", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(entityId) && !isGuidEntityId
                ? $"Business validation event on path: {entityId}"
                : "Business validation warning recorded";
        }

        switch (action.ToUpperInvariant())
        {
            case "CREATE_STAFF":
                return $"Created staff account for {targetLabel} by {actor}";
            case "UPDATE_STAFF":
                return $"Updated staff account details for {targetLabel} by {actor}";
            case "LOCK_USER":
            case "LOCK_ACCOUNT":
            case "LOCK":
                return $"Locked user account {targetLabel} by {actor}";
            case "UNLOCK_USER":
            case "UNLOCK_ACCOUNT":
            case "UNLOCK":
                return $"Unlocked user account {targetLabel} by {actor}";
            case "WARN_USER":
            case "WARN":
                return $"Issued violation warning to {targetLabel} by {actor}";
            case "BLOCK":
                return $"Blocked user account {targetLabel} by {actor}";
            case "AUTO_LOCK_USER_VIOLATION":
                return $"Automatically locked user account {targetLabel} after reaching violation threshold";
            case "UPDATE_PROFILE":
                return $"Updated own administrative profile by {actor}";
            case "CREATE_LOCATION":
                return $"Created training location {targetLabel} by {actor}";
            case "UPDATE_LOCATION":
                return $"Updated training location {targetLabel} by {actor}";
            case "DELETE_LOCATION":
                return $"Deleted training location {targetLabel} by {actor}";
            case "APPROVE_COACH_APPLICATION":
                return $"Approved coach application for {targetLabel} by {actor}";
            case "REJECT_COACH_APPLICATION":
                return $"Rejected coach application for {targetLabel} by {actor}";
            case "APPROVE_CERTIFICATE":
            case "APPROVE_COACH_CERTIFICATE":
                return $"Approved coach certificate {targetLabel} by {actor}";
            case "REJECT_CERTIFICATE":
            case "REJECT_COACH_CERTIFICATE":
                return $"Rejected coach certificate {targetLabel} by {actor}";
            case "CREATE_COACH_SUBSCRIPTION_PLAN":
            case "CREATE_SUBSCRIPTION_PLAN":
                return $"Created subscription plan {targetLabel} by {actor}";
            case "UPDATE_COACH_SUBSCRIPTION_PLAN":
            case "UPDATE_SUBSCRIPTION_PLAN":
            case "UPDATE_PRICE":
                return $"Updated subscription plan {targetLabel} by {actor}";
            case "DELETE_COACH_SUBSCRIPTION_PLAN":
            case "DELETE_SUBSCRIPTION_PLAN":
                return $"Deleted subscription plan {targetLabel} by {actor}";
            case "CREATE_PAYMENT_GATEWAY":
            case "ADD_PAYMENT_ACCOUNT":
                return $"Created payment gateway configuration {targetLabel} by {actor}";
            case "UPDATE_PAYMENT_GATEWAY":
            case "EDIT_PAYMENT_ACCOUNT":
                return $"Updated payment gateway configuration {targetLabel} by {actor}";
            case "TOGGLE_PAYMENT_GATEWAY":
                return $"Toggled payment gateway status for {targetLabel} by {actor}";
            case "ACTIVATE_PAYMENT_ACCOUNT":
                return $"Activated payment gateway configuration {targetLabel} by {actor}";
            case "DEACTIVATE_PAYMENT_ACCOUNT":
                return $"Deactivated payment gateway configuration {targetLabel} by {actor}";
            case "SET_DEFAULT_PAYMENT_GATEWAY":
                return $"Set default payment gateway to {targetLabel} by {actor}";
            case "DELETE_PAYMENT_GATEWAY":
            case "DELETE_PAYMENT_ACCOUNT":
                return $"Deleted payment gateway configuration {targetLabel} by {actor}";
            case "CREATE_REFUND_REQUEST":
                return $"Submitted refund request for {targetLabel} by {actor}";
            case "APPROVE_REFUND":
            case "APPROVE_REFUND_REQUEST":
                return $"Approved refund request for {targetLabel} by {actor}";
            case "REJECT_REFUND":
            case "REJECT_REFUND_REQUEST":
                return $"Rejected refund request for {targetLabel} by {actor}";
            case "PROCESS_PAYOUT":
            case "PAYOUT_PROCESSED":
                return $"Processed coach payout for {targetLabel} by {actor}";
            case "PAYOUT_PARTIAL":
                return $"Processed partial coach payout for {targetLabel} by {actor}";
            case "PAYOUT_FAILED":
                return $"Failed to process coach payout for {targetLabel} by {actor}";
            case "PAYOUT_BLOCKED":
                return $"Blocked coach payout for {targetLabel} by {actor}";
            case "PAYOUT_SUBMITTED":
                return $"Submitted coach payout transfer for {targetLabel} by {actor}";
            case "PAYOUT_NOOP":
                return $"Checked already-paid coach payout for {targetLabel} by {actor}";
            case "GENERATE_COACH_PAYOUT":
                return $"Generated monthly coach payout cycle by {actor}";
            case "DELETE_POST":
                return $"Deleted reported post of {targetLabel} by {actor}";
            case "RESOLVE_REPORT":
                return $"Resolved violation report for {targetLabel} by {actor}";
            case "DISMISS_REPORT":
                return $"Dismissed violation report for {targetLabel} by {actor}";
            case "REJECT_REPORT":
                return $"Rejected violation report for {targetLabel} by {actor}";
            case "APPROVE_APPEAL_UNLOCK_USER":
                return $"Approved violation appeal and unlocked account {targetLabel} by {actor}";
            case "REJECT_APPEAL":
                return $"Rejected violation appeal for {targetLabel} by {actor}";
        }

        // Default friendly format (never expose raw GUIDs)
        var formattedAction = action.Replace('_', ' ');
        if (!string.IsNullOrWhiteSpace(targetDisplayName))
        {
            return $"{formattedAction} for {targetDisplayName} by {actor}";
        }

        if (!string.IsNullOrEmpty(entityId) && !isGuidEntityId)
        {
            return $"{formattedAction} on {friendlyEntityType} {entityId} by {actor}";
        }

        return $"{formattedAction} on {friendlyEntityType} by {actor}";
    }

    private static string FormatFriendlyEntityType(string? entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType)) return "System";
        return entityType.Trim() switch
        {
            "CoachSubscriptionPlan" => "Subscription Plan",
            "RefundRequest" => "Refund Request",
            "CoachProfile" => "Coach Application",
            "CoachCertificate" => "Coach Certificate",
            "CoachUpgrade" => "Coach Upgrade",
            "PaymentGatewayConfig" => "Payment Gateway",
            _ => entityType.Trim()
        };
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
