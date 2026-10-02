using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Refunds;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class RefundRequestService : IRefundRequestService
{
    private readonly IRefundRequestRepository _refundRequestRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RefundRequestService(
        IRefundRequestRepository refundRequestRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork)
    {
        _refundRequestRepository = refundRequestRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<RefundRequestDto>> CreateRefundRequestAsync(
        Guid userId,
        CreateRefundRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ValidationException("Refund request payload cannot be null.");
        }

        var order = await _orderRepository.GetOrderByIdWithDetailsAsync(dto.OrderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        if (order.BuyerId != userId)
        {
            throw new ValidationException("You can only request refunds for your own orders.");
        }

        var orderStatus = order.OrderStatus?.ToUpperInvariant();
        if (orderStatus != "PAID" && orderStatus != "COMPLETED" && orderStatus != "ACTIVE")
        {
            throw new ValidationException($"Cannot request refund for order with status '{order.OrderStatus}'.");
        }

        var hasPending = await _refundRequestRepository.HasPendingRefundAsync(dto.OrderId, cancellationToken);
        if (hasPending)
        {
            throw new ValidationException("A pending refund request already exists for this order.");
        }

        var paymentId = order.Payments?.FirstOrDefault(p => p.Status == "PAID" || p.Status == "COMPLETED" || p.Status == "SUCCESS")?.PaymentId
                        ?? order.Payments?.FirstOrDefault()?.PaymentId;

        string? evidenceStr = null;
        if (dto.EvidenceUrls != null && dto.EvidenceUrls.Any())
        {
            evidenceStr = string.Join(";", dto.EvidenceUrls.Select(u => u.Trim()));
        }

        var now = DateTime.UtcNow;
        var refundRequest = new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            OrderId = order.OrderId,
            PaymentId = paymentId,
            RequestedBy = userId,
            RequestedAmount = order.TotalAmount ?? 0,
            Reason = dto.Reason.Trim(),
            EvidenceUrls = evidenceStr,
            Status = "PENDING",
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _refundRequestRepository.AddAsync(refundRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _refundRequestRepository.GetByIdWithDetailsAsync(refundRequest.RefundRequestId, cancellationToken) ?? refundRequest;
        return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(created), "Refund request submitted successfully.");
    }

    public async Task<ApiResponseDto<List<RefundRequestDto>>> GetMyRefundRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var list = await _refundRequestRepository.ListByUserIdAsync(userId, cancellationToken);
        var dtos = list.Select(MapToDto).ToList();
        return ApiResponseDto<List<RefundRequestDto>>.Ok(dtos, "My refund requests retrieved.");
    }

    public async Task<ApiResponseDto<List<RefundRequestDto>>> GetAllRefundRequestsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _refundRequestRepository.ListAllWithDetailsAsync(status, cancellationToken);
        var dtos = list.Select(MapToDto).ToList();
        return ApiResponseDto<List<RefundRequestDto>>.Ok(dtos, "Refund requests retrieved.");
    }

    public async Task<ApiResponseDto<RefundRequestDto>> GetRefundRequestByIdAsync(
        Guid id,
        Guid currentUserId,
        bool isAdminOrStaff = false,
        CancellationToken cancellationToken = default)
    {
        var item = await _refundRequestRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException("Refund request not found.");
        }

        if (item.RequestedBy != currentUserId && !isAdminOrStaff)
        {
            throw new ForbiddenException("You do not have permission to view this refund request.");
        }

        return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(item));
    }

    public async Task<ApiResponseDto<RefundRequestDto>> ProcessRefundRequestAsync(
        Guid id,
        Guid staffUserId,
        ProcessRefundRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            throw new ValidationException("Process payload cannot be null.");
        }

        var item = await _refundRequestRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException("Refund request not found.");
        }

        var currentStatus = item.Status?.ToUpperInvariant();
        if (currentStatus != "PENDING" && currentStatus != "IN_REVIEW")
        {
            throw new ValidationException($"Refund request has already been processed with status '{item.Status}'.");
        }

        var status = dto.Status.Trim().ToUpperInvariant();
        if (status != "APPROVED" && status != "REJECTED")
        {
            throw new ValidationException("Status must be either 'APPROVED' or 'REJECTED'.");
        }

        var now = DateTime.UtcNow;
        item.Status = status;
        item.ReviewedBy = staffUserId;
        item.ReviewedAt = now;
        item.StaffNote = dto.StaffNote?.Trim();
        item.UpdatedAt = now;

        if (status == "APPROVED")
        {
            item.ApprovedAmount = dto.ApprovedAmount ?? item.RequestedAmount;
            item.RefundTransactionRef = dto.RefundTransactionRef?.Trim();
            item.RefundedAt = now;

            if (item.Order != null)
            {
                item.Order.OrderStatus = "REFUNDED";
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(item), $"Refund request has been {status.ToLowerInvariant()}.");
    }

    private static RefundRequestDto MapToDto(RefundRequest r)
    {
        return new RefundRequestDto
        {
            RefundRequestId = r.RefundRequestId,
            OrderId = r.OrderId,
            PaymentId = r.PaymentId,
            RequestedBy = r.RequestedBy,
            RequestedByName = r.RequestedByNavigation?.FullName,
            RequestedByEmail = r.RequestedByNavigation?.Email,
            RequestedAmount = r.RequestedAmount,
            ApprovedAmount = r.ApprovedAmount,
            Reason = r.Reason,
            EvidenceUrls = ParseEvidenceUrls(r.EvidenceUrls),
            Status = r.Status,
            ReviewedBy = r.ReviewedBy,
            ReviewedByName = r.ReviewedByNavigation?.FullName,
            ReviewedAt = r.ReviewedAt,
            StaffNote = r.StaffNote,
            RefundTransactionRef = r.RefundTransactionRef,
            RequestedAt = r.RequestedAt,
            RefundedAt = r.RefundedAt,
            CreatedAt = r.CreatedAt
        };
    }

    private static List<string> ParseEvidenceUrls(string? evidenceUrls)
    {
        if (string.IsNullOrWhiteSpace(evidenceUrls))
        {
            return new List<string>();
        }

        if (evidenceUrls.TrimStart().StartsWith("["))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(evidenceUrls);
                if (list != null) return list;
            }
            catch
            {
                // Fall back to splitting
            }
        }

        return evidenceUrls
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
