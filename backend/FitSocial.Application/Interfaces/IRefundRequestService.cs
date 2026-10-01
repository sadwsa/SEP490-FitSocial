using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Refunds;

namespace FitSocial.Application.Interfaces;

public interface IRefundRequestService
{
    Task<ApiResponseDto<RefundRequestDto>> CreateRefundRequestAsync(Guid userId, CreateRefundRequestDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<List<RefundRequestDto>>> GetMyRefundRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<List<RefundRequestDto>>> GetAllRefundRequestsAsync(string? status = null, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<RefundRequestDto>> GetRefundRequestByIdAsync(Guid id, Guid currentUserId, bool isAdminOrStaff = false, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<RefundRequestDto>> ProcessRefundRequestAsync(Guid id, Guid staffUserId, ProcessRefundRequestDto dto, CancellationToken cancellationToken = default);
}
