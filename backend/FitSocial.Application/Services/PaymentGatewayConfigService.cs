using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.PaymentGateway;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class PaymentGatewayConfigService : IPaymentGatewayConfigService
{
    private readonly IPaymentGatewayConfigRepository _configRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;

    public PaymentGatewayConfigService(
        IPaymentGatewayConfigRepository configRepository,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService)
    {
        _configRepository = configRepository;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponseDto<List<PaymentGatewayConfigDto>>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _configRepository.GetAllConfigsAsync(cancellationToken);
        var dtos = configs.Select(MapToDto).ToList();
        return ApiResponseDto<List<PaymentGatewayConfigDto>>.Ok(dtos, "Payment gateway configs retrieved.");
    }

    public async Task<ApiResponseDto<PaymentGatewayConfigDto>> GetConfigByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        return ApiResponseDto<PaymentGatewayConfigDto>.Ok(MapToDto(config));
    }

    public async Task<ApiResponseDto<PaymentGatewayConfigDto>> CreateConfigAsync(
        Guid adminUserId,
        CreatePaymentGatewayConfigDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ValidationException("Config payload cannot be null.");
        }

        var now = DateTime.UtcNow;
        var config = new PaymentGatewayConfig
        {
            GatewayId = Guid.NewGuid(),
            GatewayName = dto.GatewayName.Trim(),
            ClientId = dto.ClientId.Trim(),
            EncryptedApiKey = _encryptionService.Encrypt(dto.ApiKey.Trim()),
            EncryptedChecksumKey = _encryptionService.Encrypt(dto.ChecksumKey.Trim()),
            WebhookUrl = dto.WebhookUrl?.Trim(),
            IsActive = dto.IsActive,
            IsPayout = dto.IsPayout,
            UpdatedBy = adminUserId,
            UpdatedAt = now
        };

        // Single-active-per-purpose invariant: at most one active collection
        // gateway and one active payout gateway. A new row starts Inactive
        // unless nothing of the same purpose is active, then it becomes active.
        var existing = await _configRepository.GetAllConfigsAsync(cancellationToken)
            ?? new List<PaymentGatewayConfig>();
        config.IsActive = !existing.Any(c => c.IsActive == true && IsPayoutOf(c) == config.IsPayout);

        await _configRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "ADD_PAYMENT_ACCOUNT",
            entityId: config.GatewayId,
            description: $"Payment account '{config.GatewayName}' was added ({(config.IsPayout == true ? "payout" : "collection")}, {(config.IsActive == true ? "active" : "inactive")}).",
            oldValue: null,
            newValue: new
            {
                gatewayName = config.GatewayName,
                clientId = config.ClientId,
                webhookUrl = config.WebhookUrl,
                isActive = config.IsActive,
                isPayout = config.IsPayout,
                apiKeyProvided = true,
                checksumKeyProvided = true
            },
            cancellationToken);

        return ApiResponseDto<PaymentGatewayConfigDto>.Ok(MapToDto(config), "Payment gateway config created successfully.");
    }

    public async Task<ApiResponseDto<PaymentGatewayConfigDto>> UpdateConfigAsync(
        Guid id,
        Guid adminUserId,
        UpdatePaymentGatewayConfigDto dto,
        CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        var oldValues = Snapshot(config);

        if (!string.IsNullOrWhiteSpace(dto.GatewayName)) config.GatewayName = dto.GatewayName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ClientId)) config.ClientId = dto.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ApiKey)) config.EncryptedApiKey = _encryptionService.Encrypt(dto.ApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(dto.ChecksumKey)) config.EncryptedChecksumKey = _encryptionService.Encrypt(dto.ChecksumKey.Trim());
        if (dto.WebhookUrl != null) config.WebhookUrl = dto.WebhookUrl.Trim();
        if (dto.IsActive.HasValue) config.IsActive = dto.IsActive.Value;
        if (dto.IsPayout.HasValue) config.IsPayout = dto.IsPayout.Value;

        // Single-active-per-purpose invariant: only one active gateway per
        // channel. Turning this one on while another of the same channel is
        // active is rejected — staff must deactivate the current one first.
        if (config.IsActive == true)
        {
            await EnsureNoOtherActiveAsync(id, IsPayoutOf(config), cancellationToken);
        }

        config.UpdatedBy = adminUserId;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "EDIT_PAYMENT_ACCOUNT",
            entityId: config.GatewayId,
            description: $"Payment account '{config.GatewayName}' was edited.",
            oldValue: oldValues,
            newValue: Snapshot(config, dto),
            cancellationToken);

        return ApiResponseDto<PaymentGatewayConfigDto>.Ok(MapToDto(config), "Payment gateway config updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeleteConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        var oldValues = Snapshot(config);
        var gatewayName = config.GatewayName;

        // Soft-delete / deactivate
        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "DELETE_PAYMENT_ACCOUNT",
            entityId: config.GatewayId,
            description: $"Payment account '{gatewayName}' was deleted (deactivated).",
            oldValue: oldValues,
            newValue: Snapshot(config),
            cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config deactivated successfully.");
    }

    public async Task<ApiResponseDto<bool>> ActivateConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        await EnsureNoOtherActiveAsync(id, IsPayoutOf(config), cancellationToken);

        var oldValues = Snapshot(config);

        config.IsActive = true;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "ACTIVATE_PAYMENT_ACCOUNT",
            entityId: config.GatewayId,
            description: $"Payment account '{config.GatewayName}' was activated for {(IsPayoutOf(config) ? "payout" : "collection")}.",
            oldValue: oldValues,
            newValue: Snapshot(config),
            cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config activated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeactivateConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        var oldValues = Snapshot(config);

        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            action: "DEACTIVATE_PAYMENT_ACCOUNT",
            entityId: config.GatewayId,
            description: $"Payment account '{config.GatewayName}' was deactivated.",
            oldValue: oldValues,
            newValue: Snapshot(config),
            cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config deactivated successfully.");
    }

    /// <summary>
    /// Non-sensitive projection of a gateway config used for audit trails.
    /// API key / checksum key are NEVER included - only whether they were supplied,
    /// so secrets never reach the "AuditLogs" table.
    /// </summary>
    private static object Snapshot(PaymentGatewayConfig c, UpdatePaymentGatewayConfigDto? dto = null)
    {
        return new
        {
            gatewayName = c.GatewayName,
            clientId = c.ClientId,
            webhookUrl = c.WebhookUrl,
            isActive = c.IsActive,
            isPayout = c.IsPayout,
            apiKeyPresent = c.EncryptedApiKey != null,
            checksumKeyPresent = c.EncryptedChecksumKey != null,
            apiKeyRotated = dto != null && !string.IsNullOrWhiteSpace(dto.ApiKey),
            checksumKeyRotated = dto != null && !string.IsNullOrWhiteSpace(dto.ChecksumKey)
        };
    }

    /// <summary>
    /// UC-36.x: every Admin action on a payment account (add / edit / activate /
    /// deactivate / delete) is traced in the shared "AuditLogs" table. Actor,
    /// role and IP are auto-resolved from the current request by IAuditLogService.
    /// </summary>
    private async Task WriteAuditAsync(
        string action,
        Guid entityId,
        string description,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        try
        {
            await _auditLogService.LogAsync(
                actorAccountId: null, // Auto-resolved from ICurrentUserContext
                action: action,
                entityType: "PaymentGatewayConfig",
                entityId: entityId.ToString(),
                description: description,
                oldValue: oldValue,
                newValue: newValue,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Audit log failure is non-fatal - the business operation already succeeded.
        }
    }

    private PaymentGatewayConfigDto MapToDto(PaymentGatewayConfig c)
    {
        string? plainApiKey = null;
        string? plainChecksumKey = null;

        try
        {
            if (c.EncryptedApiKey != null)
            {
                plainApiKey = _encryptionService.Decrypt(c.EncryptedApiKey);
            }
        }
        catch { }

        try
        {
            if (c.EncryptedChecksumKey != null)
            {
                plainChecksumKey = _encryptionService.Decrypt(c.EncryptedChecksumKey);
            }
        }
        catch { }

        return new PaymentGatewayConfigDto
        {
            GatewayId = c.GatewayId,
            GatewayName = c.GatewayName,
            ClientId = c.ClientId,
            ClientIdMasked = MaskKey(c.ClientId),
            ApiKey = plainApiKey,
            ApiKeyMasked = MaskKey(plainApiKey),
            ChecksumKey = plainChecksumKey,
            ChecksumKeyMasked = MaskKey(plainChecksumKey),
            WebhookUrl = c.WebhookUrl,
            IsActive = c.IsActive ?? true,
            IsPayout = c.IsPayout ?? false,
            UpdatedBy = c.UpdatedBy,
            UpdatedAt = c.UpdatedAt
        };
    }

    private static string MaskKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "********";
        if (value.Length <= 8) return new string('*', value.Length);
        return $"{value[..4]}...{value[^4..]}";
    }

    private static bool IsPayoutOf(PaymentGatewayConfig config)
    {
        return config.IsPayout == true;
    }

    private async Task EnsureNoOtherActiveAsync(Guid exceptId, bool isPayout, CancellationToken cancellationToken)
    {
        var all = await _configRepository.GetAllConfigsAsync(cancellationToken)
            ?? new List<PaymentGatewayConfig>();
        var other = all.FirstOrDefault(c => c.GatewayId != exceptId && c.IsActive == true && IsPayoutOf(c) == isPayout);
        if (other != null)
        {
            var channel = isPayout ? "payout" : "collection";
            throw new ValidationException(
                $"Gateway '{other.GatewayName}' is currently active for {channel}. Please deactivate it first — only one {channel} gateway can be active at a time.");
        }
    }
}
