using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.PaymentGateway;

namespace FitSocial.Application.Interfaces;

public interface IPaymentGatewayConfigService
{
    Task<ApiResponseDto<List<PaymentGatewayConfigDto>>> GetAllConfigsAsync(CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PaymentGatewayConfigDto>> GetConfigByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PaymentGatewayConfigDto>> CreateConfigAsync(Guid adminUserId, CreatePaymentGatewayConfigDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PaymentGatewayConfigDto>> UpdateConfigAsync(Guid id, Guid adminUserId, UpdatePaymentGatewayConfigDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> DeleteConfigAsync(Guid id, CancellationToken cancellationToken = default);
}
