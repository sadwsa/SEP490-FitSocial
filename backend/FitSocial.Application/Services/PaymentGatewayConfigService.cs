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

    public PaymentGatewayConfigService(
        IPaymentGatewayConfigRepository configRepository,
        IEncryptionService encryptionService,
        IUnitOfWork unitOfWork)
    {
        _configRepository = configRepository;
        _encryptionService = encryptionService;
        _unitOfWork = unitOfWork;
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
            UpdatedBy = adminUserId,
            UpdatedAt = now
        };

        // Single-active invariant: the system collects through exactly one
        // gateway. A new gateway starts Inactive unless none is active,
        // in which case it becomes the active one.
        var existing = await _configRepository.GetAllConfigsAsync(cancellationToken);
        config.IsActive = !existing.Any(c => c.IsActive == true);

        await _configRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        if (!string.IsNullOrWhiteSpace(dto.GatewayName)) config.GatewayName = dto.GatewayName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ClientId)) config.ClientId = dto.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.ApiKey)) config.EncryptedApiKey = _encryptionService.Encrypt(dto.ApiKey.Trim());
        if (!string.IsNullOrWhiteSpace(dto.ChecksumKey)) config.EncryptedChecksumKey = _encryptionService.Encrypt(dto.ChecksumKey.Trim());
        if (dto.WebhookUrl != null) config.WebhookUrl = dto.WebhookUrl.Trim();
        if (dto.IsActive.HasValue) config.IsActive = dto.IsActive.Value;

        // Single-active invariant: only one gateway may be active at a time.
        // Turning this one on while another is active is rejected — staff must
        // deactivate the current one first.
        if (config.IsActive == true)
        {
            await EnsureNoOtherActiveAsync(id, cancellationToken);
        }

        config.UpdatedBy = adminUserId;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<PaymentGatewayConfigDto>.Ok(MapToDto(config), "Payment gateway config updated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeleteConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        // Soft-delete / deactivate
        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config deactivated successfully.");
    }

    public async Task<ApiResponseDto<bool>> ActivateConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        await EnsureNoOtherActiveAsync(id, cancellationToken);

        config.IsActive = true;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config activated successfully.");
    }

    public async Task<ApiResponseDto<bool>> DeactivateConfigAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _configRepository.GetByIdAsync(id, cancellationToken);
        if (config == null)
        {
            throw new NotFoundException("Payment gateway config not found.");
        }

        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<bool>.Ok(true, "Payment gateway config deactivated successfully.");
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

    private async Task EnsureNoOtherActiveAsync(Guid exceptId, CancellationToken cancellationToken)
    {
        var all = await _configRepository.GetAllConfigsAsync(cancellationToken);
        var other = all.FirstOrDefault(c => c.GatewayId != exceptId && c.IsActive == true);
        if (other != null)
        {
            throw new ValidationException(
                $"Gateway '{other.GatewayName}' is currently active. Please deactivate it first — only one payment gateway can be active at a time.");
        }
    }
}
