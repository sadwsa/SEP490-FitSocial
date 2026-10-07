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
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class RefundRequestService : IRefundRequestService
{
    private readonly IRefundRequestRepository _refundRequestRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogRepository? _auditLogs;
    private readonly INotificationRepository? _notifications;
    private readonly ITrainingPlanRepository? _trainingPlans;
    private readonly ITermsAndPolicyRepository? _terms;
    private readonly IUserRepository? _users;
    private readonly IChatRealtimeNotifier? _realtimeNotifier;

    public RefundRequestService(
        IRefundRequestRepository refundRequestRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IAuditLogRepository? auditLogs = null,
        INotificationRepository? notifications = null,
        ITrainingPlanRepository? trainingPlans = null,
        ITermsAndPolicyRepository? terms = null,
        IUserRepository? users = null,
        IChatRealtimeNotifier? realtimeNotifier = null)
    {
        _refundRequestRepository = refundRequestRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _auditLogs = auditLogs;
        _notifications = notifications;
        _trainingPlans = trainingPlans;
        _terms = terms;
        _users = users;
        _realtimeNotifier = realtimeNotifier;
    }

    private async Task<string> GetStaffNameAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        if (_users != null)
        {
            try
            {
                var user = await _users.GetByIdAsync(staffUserId, cancellationToken);
                if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                {
                    return user.FullName;
                }
            }
            catch
            {
                // Fallback to default
            }
        }
        return "Admin / Staff";
    }

    public async Task<ApiResponseDto<RefundRequestDto>> CreateRefundRequestAsync(
        Guid userId,
        CreateRefundRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ValidationException("User is not authenticated.");
        }

        if (dto == null)
        {
            throw new ValidationException("Refund request payload cannot be null.");
        }

        if (dto.OrderId == Guid.Empty)
        {
            throw new ValidationException("Order ID is required.");
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
        if (orderStatus == PaymentConstants.OrderStatusRefunded)
        {
            throw new BusinessException("This order has already been refunded.");
        }

        if (orderStatus != PaymentConstants.OrderStatusPaid
            && orderStatus != PaymentConstants.OrderStatusCompleted
            && orderStatus != "ACTIVE")
        {
            throw new ValidationException($"Cannot request refund for order with status '{order.OrderStatus}'.");
        }

        // LEVEL 2 Snapshot: Validate 7-day refund policy based on UTC purchase time
        var purchaseTime = order.Payments?.FirstOrDefault(p =>
                               p.Status == PaymentConstants.PaymentStatusSuccess
                               || p.Status == PaymentConstants.OrderStatusPaid
                               || p.Status == PaymentConstants.OrderStatusCompleted)?.ProcessedAt
                           ?? order.CreatedAt;

        if (!purchaseTime.HasValue)
        {
            throw new BusinessException("Cannot determine the purchase date.");
        }

        var refundDeadline = purchaseTime.Value.AddDays(7);
        if (DateTime.UtcNow > refundDeadline)
        {
            throw new BusinessException("The refund request period has expired (refunds are only permitted within 7 days of purchase).");
        }

        // Mutual Consistency: Check if coach payout has already been processed for this order
        var isAlreadyPaidOut = order.PayoutItems.Any(pi =>
            pi.Payout != null && string.Equals(pi.Payout.Status, PayoutConstants.StatusProcessed, StringComparison.OrdinalIgnoreCase));
        if (isAlreadyPaidOut)
        {
            throw new BusinessException("This transaction is no longer eligible for refund because the payout to the coach has already been processed.");
        }

        var hasPending = await _refundRequestRepository.HasPendingRefundAsync(dto.OrderId, cancellationToken);
        if (hasPending)
        {
            throw new ValidationException("A pending refund request already exists for this order.");
        }

        var hasApproved = await _refundRequestRepository.HasApprovedRefundAsync(dto.OrderId, cancellationToken);
        if (hasApproved)
        {
            throw new BusinessException("This order has already been refunded.");
        }

        var paymentId = order.Payments?.FirstOrDefault(p =>
                            p.Status == PaymentConstants.PaymentStatusSuccess
                            || p.Status == PaymentConstants.OrderStatusPaid
                            || p.Status == PaymentConstants.OrderStatusCompleted)?.PaymentId
                        ?? order.Payments?.FirstOrDefault()?.PaymentId;

        string? evidenceStr = null;
        if (dto.EvidenceUrls != null && dto.EvidenceUrls.Any())
        {
            evidenceStr = string.Join(";", dto.EvidenceUrls.Select(u => u.Trim()));
        }

        // Policy / Term version snapshot at purchase time
        Guid? termId = null;
        if (_terms != null)
        {
            try
            {
                var latestTerm = await _terms.GetLatestAsync(cancellationToken);
                termId = latestTerm?.TermId;
            }
            catch
            {
                // Fallback if terms service is unavailable
            }
        }

        var now = DateTime.UtcNow;

        // Purchase financial snapshot: Always use Order.TotalAmount / OrderDetail price at purchase
        var snapshotPaidAmount = order.TotalAmount ?? order.OrderDetails.FirstOrDefault()?.PackagePrice ?? 0;

        var refundRequest = new RefundRequest
        {
            RefundRequestId = Guid.NewGuid(),
            OrderId = order.OrderId,
            PaymentId = paymentId,
            RequestedBy = userId,
            TermId = termId,
            RequestedAmount = snapshotPaidAmount,
            Reason = dto.Reason.Trim(),
            EvidenceUrls = evidenceStr,
            Status = "PENDING",
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Transactional execution for refund creation, dual notification, and audit logging
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _refundRequestRepository.AddAsync(refundRequest, cancellationToken);

            var firstDetail = order.OrderDetails.FirstOrDefault();
            var packageName = firstDetail?.PackageTitle ?? firstDetail?.Package?.Title ?? "Training Package";
            var buyerName = order.Buyer?.FullName ?? order.Buyer?.Email ?? "Trainee";
            var coachId = order.CoachId ?? firstDetail?.Package?.CoachId;
            var coachName = order.Coach?.Coach?.FullName ?? firstDetail?.CoachName ?? "Assigned Coach";

            var notificationsToSend = new List<(Guid RecipientId, string Type, string Message)>();

            // 1. Notification for Buyer (Trainee)
            var buyerMsg = $"Your refund request for \"{packageName}\" has been submitted successfully and is pending review.";
            if (_notifications != null)
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ActorId = userId,
                    ReferenceId = refundRequest.RefundRequestId,
                    Type = "REFUND_SUBMITTED",
                    Description = buyerMsg,
                    IsRead = false,
                    CreatedAt = now
                }, cancellationToken);
            }
            notificationsToSend.Add((userId, "REFUND_SUBMITTED", buyerMsg));

            // 2. Notification for Seller (Coach)
            if (coachId.HasValue && coachId.Value != Guid.Empty && coachId.Value != userId)
            {
                var coachMsg = $"A refund request has been submitted for your training package \"{packageName}\" and is pending review.";
                if (_notifications != null)
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = coachId.Value,
                        ActorId = userId,
                        ReferenceId = refundRequest.RefundRequestId,
                        Type = "REFUND_REQUESTED",
                        Description = coachMsg,
                        IsRead = false,
                        CreatedAt = now
                    }, cancellationToken);
                }
                notificationsToSend.Add((coachId.Value, "REFUND_REQUESTED", coachMsg));
            }

            // 3. AuditLog for CREATE_REFUND_REQUEST
            if (_auditLogs != null)
            {
                var auditDesc = $"Refund request created.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOriginal Paid Amount: {snapshotPaidAmount:N0} VND\nPurchase Date: {purchaseTime.Value:dd/MM/yyyy HH:mm}\nRequested At: {now:dd/MM/yyyy HH:mm}\nStatus: PENDING\nReason: {refundRequest.Reason}";
                await _auditLogs.AddAsync(new AuditLog
                {
                    ActorAccountId = userId,
                    Action = "CREATE_REFUND_REQUEST",
                    EntityType = "RefundRequest",
                    EntityId = refundRequest.RefundRequestId.ToString(),
                    OldValue = null,
                    NewValue = JsonSerializer.Serialize(new
                    {
                        description = auditDesc,
                        buyer = buyerName,
                        seller = coachName,
                        package = packageName,
                        paidAmount = snapshotPaidAmount,
                        purchaseDate = purchaseTime.Value,
                        requestedAt = now,
                        status = "PENDING",
                        reason = refundRequest.Reason
                    }),
                    CreatedAt = now
                }, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // 4. Realtime notifications sent strictly AFTER transaction commit
            if (_realtimeNotifier != null)
            {
                foreach (var (recipientId, type, msg) in notificationsToSend)
                {
                    try
                    {
                        await _realtimeNotifier.SendNotificationAsync(recipientId, new DTOs.Notifications.NotificationDto
                        {
                            NotificationId = Guid.NewGuid(),
                            Type = type,
                            SenderId = userId,
                            SenderName = buyerName,
                            MessagePreview = msg,
                            CreatedAt = now,
                            IsRead = false
                        });
                    }
                    catch
                    {
                        // Realtime push non-fatal
                    }
                }
            }

            var created = await _refundRequestRepository.GetByIdWithDetailsAsync(refundRequest.RefundRequestId, cancellationToken) ?? refundRequest;
            return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(created), "Refund request submitted successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
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

    public async Task<ApiResponseDto<PagedResultDto<RefundRequestDto>>> GetPagedRefundRequestsAsync(
        GetRefundRequestsQueryDto query,
        CancellationToken cancellationToken = default)
    {
        query ??= new GetRefundRequestsQueryDto();

        var (items, totalCount) = await _refundRequestRepository.GetPagedRefundRequestsAsync(
            query.Status,
            query.FromDate,
            query.ToDate,
            query.SearchTerm,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        var pagedResult = PagedResultDto<RefundRequestDto>.Create(dtos, totalCount, query.PageNumber, query.PageSize);
        return ApiResponseDto<PagedResultDto<RefundRequestDto>>.Ok(pagedResult, "Refund requests retrieved successfully.");
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

    public async Task<ApiResponseDto<RefundRequestDto>> ApproveRefundRequestAsync(
        Guid id,
        Guid staffUserId,
        ApproveRefundRequestDto dto,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("Refund Request ID is required.");
        }

        if (staffUserId == Guid.Empty)
        {
            throw new ValidationException("Staff user ID is required.");
        }

        // LEVEL 1 Concurrency Snapshot: Transactional execution with optimistic concurrency token check
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var item = await _refundRequestRepository.GetByIdWithDetailsAsync(id, cancellationToken);
            if (item == null)
            {
                throw new NotFoundException("Refund request not found.");
            }

            var currentStatus = item.Status?.Trim().ToUpperInvariant();
            if (currentStatus != "PENDING" && currentStatus != "IN_REVIEW")
            {
                throw new ValidationException($"This refund request has already been processed with status '{item.Status}'.");
            }

            var order = item.Order;
            if (order == null)
            {
                order = await _orderRepository.GetOrderByIdWithDetailsAsync(item.OrderId, cancellationToken);
            }

            if (order == null)
            {
                throw new NotFoundException("Associated order not found.");
            }

            if (string.Equals(order.OrderStatus, PaymentConstants.OrderStatusRefunded, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("This order has already been refunded.");
            }

            // Mutual Consistency: Check coach payout status
            var isAlreadyPaidOut = order.PayoutItems.Any(pi =>
                pi.Payout != null && string.Equals(pi.Payout.Status, PayoutConstants.StatusProcessed, StringComparison.OrdinalIgnoreCase));
            if (isAlreadyPaidOut)
            {
                throw new BusinessException("This transaction is no longer eligible for refund because the payout to the coach has already been processed.");
            }

            // Check Payment state
            var payment = item.Payment
                          ?? order.Payments?.FirstOrDefault(p => p.PaymentId == item.PaymentId)
                          ?? order.Payments?.FirstOrDefault();

            if (payment != null && string.Equals(payment.Status, PaymentConstants.PaymentStatusRefunded, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("This payment has already been refunded.");
            }

            // Financial snapshot amount: Stored at transaction time
            var snapshotAmount = item.RequestedAmount ?? order.TotalAmount ?? 0;
            var approvedAmount = dto?.ApprovedAmount is > 0
                ? dto.ApprovedAmount.Value
                : snapshotAmount;

            if (approvedAmount > (order.TotalAmount ?? snapshotAmount))
            {
                throw new ValidationException($"Approved amount ({approvedAmount:N0} VND) cannot exceed the original paid amount ({order.TotalAmount:N0} VND).");
            }

            // Idempotent refund transaction reference
            var txRef = !string.IsNullOrWhiteSpace(dto?.RefundTransactionRef)
                ? dto.RefundTransactionRef.Trim()
                : (!string.IsNullOrWhiteSpace(item.RefundTransactionRef)
                    ? item.RefundTransactionRef
                    : $"REF-{item.RefundRequestId:N}"[..18].ToUpperInvariant());

            var now = DateTime.UtcNow;

            // Update RefundRequest entity (Concurrency token on Status property ensures atomic transition)
            item.Status = "APPROVED";
            item.ApprovedAmount = approvedAmount;
            item.RefundTransactionRef = txRef;
            item.ReviewedBy = staffUserId;
            item.ReviewedAt = now;
            item.RefundedAt = now;
            item.StaffNote = dto?.StaffNote?.Trim();
            item.UpdatedAt = now;

            // Update Order state
            order.OrderStatus = PaymentConstants.OrderStatusRefunded;

            // Update Payment state
            if (payment != null)
            {
                payment.Status = PaymentConstants.PaymentStatusRefunded;
                payment.UpdatedAt = now;
            }

            // Revoke active training plan enrollment associated with the order detail
            if (_trainingPlans != null && order.OrderDetails != null)
            {
                foreach (var detail in order.OrderDetails)
                {
                    try
                    {
                        var activePlan = await _trainingPlans.GetActiveByOrderDetailsIdAsync(detail.OrderDetailsId, cancellationToken);
                        if (activePlan != null)
                        {
                            activePlan.Status = "CANCELLED";
                            activePlan.UpdatedAt = now;
                        }
                    }
                    catch
                    {
                        // Plan cancellation non-fatal to refund
                    }
                }
            }

            var firstDetail = order.OrderDetails?.FirstOrDefault();
            var packageName = firstDetail?.PackageTitle ?? firstDetail?.Package?.Title ?? "Training Package";
            var buyerName = item.RequestedByNavigation?.FullName ?? order.Buyer?.FullName ?? item.RequestedByNavigation?.Email ?? "Buyer";
            var coachId = order.CoachId ?? firstDetail?.Package?.CoachId;
            var coachName = order.Coach?.Coach?.FullName ?? firstDetail?.CoachName ?? "Assigned Coach";
            var orderRef = $"#ORD-{order.OrderId.ToString("N")[..8].ToUpperInvariant()}";
            var staffName = await GetStaffNameAsync(staffUserId, cancellationToken);

            var notificationsToSend = new List<(Guid RecipientId, string Type, string Message)>();

            // 1. Notification for Buyer
            var buyerMsg = $"Your refund request for \"{packageName}\" has been approved. The refund amount is {approvedAmount:N0} VND.";
            if (_notifications != null)
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = item.RequestedBy,
                    ActorId = staffUserId,
                    ReferenceId = item.RefundRequestId,
                    Type = "REFUND_APPROVED",
                    Description = buyerMsg,
                    IsRead = false,
                    CreatedAt = now
                }, cancellationToken);
            }
            notificationsToSend.Add((item.RequestedBy, "REFUND_APPROVED", buyerMsg));

            // 2. Notification for Seller (Coach)
            if (coachId.HasValue && coachId.Value != Guid.Empty && coachId.Value != item.RequestedBy)
            {
                var coachMsg = $"A refund request for your training package \"{packageName}\" has been approved. The refund amount is {approvedAmount:N0} VND.";
                if (_notifications != null)
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = coachId.Value,
                        ActorId = staffUserId,
                        ReferenceId = item.RefundRequestId,
                        Type = "REFUND_APPROVED",
                        Description = coachMsg,
                        IsRead = false,
                        CreatedAt = now
                    }, cancellationToken);
                }
                notificationsToSend.Add((coachId.Value, "REFUND_APPROVED", coachMsg));
            }

            // 3. Audit log for APPROVE_REFUND
            if (_auditLogs != null)
            {
                var auditDesc = $"Refund request approved.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOrder: {orderRef}\nOriginal Paid Amount: {snapshotAmount:N0} VND\nRefund Amount: {approvedAmount:N0} VND\nPurchase Date: {(order.CreatedAt.HasValue ? order.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—")}\nRequested At: {(item.RequestedAt.HasValue ? item.RequestedAt.Value.ToString("dd/MM/yyyy HH:mm") : now.ToString("dd/MM/yyyy HH:mm"))}\nProcessed At: {now:dd/MM/yyyy HH:mm}\nPrevious Status: PENDING\nNew Status: APPROVED\nProcessed By: {staffName}";
                await _auditLogs.AddAsync(new AuditLog
                {
                    ActorAccountId = staffUserId,
                    Action = "APPROVE_REFUND",
                    EntityType = "RefundRequest",
                    EntityId = item.RefundRequestId.ToString(),
                    OldValue = JsonSerializer.Serialize(new { status = currentStatus, requestedAmount = item.RequestedAmount }),
                    NewValue = JsonSerializer.Serialize(new
                    {
                        description = auditDesc,
                        buyer = buyerName,
                        seller = coachName,
                        package = packageName,
                        order = orderRef,
                        originalPaidAmount = snapshotAmount,
                        refundAmount = approvedAmount,
                        status = "APPROVED",
                        approvedAmount,
                        refundTransactionRef = txRef,
                        staffNote = item.StaffNote,
                        processedBy = staffName,
                        processedAt = now
                    }),
                    Ipaddress = ipAddress,
                    CreatedAt = now
                }, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // 4. Realtime notifications strictly AFTER transaction commit
            if (_realtimeNotifier != null)
            {
                foreach (var (recipientId, type, msg) in notificationsToSend)
                {
                    try
                    {
                        await _realtimeNotifier.SendNotificationAsync(recipientId, new DTOs.Notifications.NotificationDto
                        {
                            NotificationId = Guid.NewGuid(),
                            Type = type,
                            SenderId = staffUserId,
                            SenderName = staffName,
                            MessagePreview = msg,
                            CreatedAt = now,
                            IsRead = false
                        });
                    }
                    catch
                    {
                        // Realtime push non-fatal
                    }
                }
            }

            return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(item), "Refund request approved and processed successfully.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw new BusinessException("This refund request was processed by another staff member. Please refresh the page.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ApiResponseDto<RefundRequestDto>> RejectRefundRequestAsync(
        Guid id,
        Guid staffUserId,
        RejectRefundRequestDto dto,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ValidationException("Refund Request ID is required.");
        }

        if (staffUserId == Guid.Empty)
        {
            throw new ValidationException("Staff user ID is required.");
        }

        if (dto == null || string.IsNullOrWhiteSpace(dto.StaffNote))
        {
            throw new ValidationException("A rejection note is required explaining why the request was rejected.");
        }

        // LEVEL 1 Concurrency Snapshot: Transactional execution with optimistic concurrency token check
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var item = await _refundRequestRepository.GetByIdWithDetailsAsync(id, cancellationToken);
            if (item == null)
            {
                throw new NotFoundException("Refund request not found.");
            }

            var currentStatus = item.Status?.Trim().ToUpperInvariant();
            if (currentStatus != "PENDING" && currentStatus != "IN_REVIEW")
            {
                throw new ValidationException($"This refund request has already been processed with status '{item.Status}'.");
            }

            var now = DateTime.UtcNow;

            item.Status = "REJECTED";
            item.ReviewedBy = staffUserId;
            item.ReviewedAt = now;
            item.StaffNote = dto.StaffNote.Trim();
            item.UpdatedAt = now;

            var order = item.Order ?? await _orderRepository.GetOrderByIdWithDetailsAsync(item.OrderId, cancellationToken);
            var firstDetail = order?.OrderDetails?.FirstOrDefault();
            var packageName = firstDetail?.PackageTitle ?? firstDetail?.Package?.Title ?? "Training Package";
            var buyerName = item.RequestedByNavigation?.FullName ?? order?.Buyer?.FullName ?? item.RequestedByNavigation?.Email ?? "Buyer";
            var coachId = order?.CoachId ?? firstDetail?.Package?.CoachId;
            var coachName = order?.Coach?.Coach?.FullName ?? firstDetail?.CoachName ?? "Assigned Coach";
            var orderRef = order != null ? $"#ORD-{order.OrderId.ToString("N")[..8].ToUpperInvariant()}" : $"#ORD-{item.OrderId.ToString("N")[..8].ToUpperInvariant()}";
            var staffName = await GetStaffNameAsync(staffUserId, cancellationToken);
            var snapshotAmount = item.RequestedAmount ?? order?.TotalAmount ?? 0;
            var rejectionReason = dto.StaffNote.Trim();

            var notificationsToSend = new List<(Guid RecipientId, string Type, string Message)>();

            // 1. Notification for Buyer
            var buyerMsg = !string.IsNullOrWhiteSpace(rejectionReason)
                ? $"Your refund request for \"{packageName}\" has been rejected.\nReason: {rejectionReason}"
                : $"Your refund request for \"{packageName}\" has been rejected.";

            if (_notifications != null)
            {
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = item.RequestedBy,
                    ActorId = staffUserId,
                    ReferenceId = item.RefundRequestId,
                    Type = "REFUND_REJECTED",
                    Description = buyerMsg,
                    IsRead = false,
                    CreatedAt = now
                }, cancellationToken);
            }
            notificationsToSend.Add((item.RequestedBy, "REFUND_REJECTED", buyerMsg));

            // 2. Notification for Seller (Coach)
            if (coachId.HasValue && coachId.Value != Guid.Empty && coachId.Value != item.RequestedBy)
            {
                var coachMsg = !string.IsNullOrWhiteSpace(rejectionReason)
                    ? $"The refund request for your training package \"{packageName}\" has been rejected.\nReason: {rejectionReason}"
                    : $"The refund request for your training package \"{packageName}\" has been rejected.";

                if (_notifications != null)
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = coachId.Value,
                        ActorId = staffUserId,
                        ReferenceId = item.RefundRequestId,
                        Type = "REFUND_REJECTED",
                        Description = coachMsg,
                        IsRead = false,
                        CreatedAt = now
                    }, cancellationToken);
                }
                notificationsToSend.Add((coachId.Value, "REFUND_REJECTED", coachMsg));
            }

            // 3. Audit log for REJECT_REFUND
            if (_auditLogs != null)
            {
                var auditDesc = $"Refund request rejected.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOrder: {orderRef}\nOriginal Paid Amount: {snapshotAmount:N0} VND\nPurchase Date: {(order?.CreatedAt.HasValue == true ? order.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—")}\nRequested At: {(item.RequestedAt.HasValue ? item.RequestedAt.Value.ToString("dd/MM/yyyy HH:mm") : now.ToString("dd/MM/yyyy HH:mm"))}\nProcessed At: {now:dd/MM/yyyy HH:mm}\nPrevious Status: PENDING\nNew Status: REJECTED\nProcessed By: {staffName}\nRejection Reason: {rejectionReason}";
                await _auditLogs.AddAsync(new AuditLog
                {
                    ActorAccountId = staffUserId,
                    Action = "REJECT_REFUND",
                    EntityType = "RefundRequest",
                    EntityId = item.RefundRequestId.ToString(),
                    OldValue = JsonSerializer.Serialize(new { status = currentStatus }),
                    NewValue = JsonSerializer.Serialize(new
                    {
                        description = auditDesc,
                        buyer = buyerName,
                        seller = coachName,
                        package = packageName,
                        order = orderRef,
                        status = "REJECTED",
                        staffNote = item.StaffNote,
                        processedBy = staffName,
                        rejectionReason,
                        processedAt = now
                    }),
                    Ipaddress = ipAddress,
                    CreatedAt = now
                }, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // 4. Realtime notifications strictly AFTER transaction commit
            if (_realtimeNotifier != null)
            {
                foreach (var (recipientId, type, msg) in notificationsToSend)
                {
                    try
                    {
                        await _realtimeNotifier.SendNotificationAsync(recipientId, new DTOs.Notifications.NotificationDto
                        {
                            NotificationId = Guid.NewGuid(),
                            Type = type,
                            SenderId = staffUserId,
                            SenderName = staffName,
                            MessagePreview = msg,
                            CreatedAt = now,
                            IsRead = false
                        });
                    }
                    catch
                    {
                        // Realtime push non-fatal
                    }
                }
            }

            return ApiResponseDto<RefundRequestDto>.Ok(MapToDto(item), "Refund request has been rejected.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw new BusinessException("This refund request was processed by another staff member. Please refresh the page.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
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

        var status = dto.Status.Trim().ToUpperInvariant();
        if (status == "APPROVED")
        {
            return await ApproveRefundRequestAsync(id, staffUserId, new ApproveRefundRequestDto
            {
                ApprovedAmount = dto.ApprovedAmount,
                StaffNote = dto.StaffNote,
                RefundTransactionRef = dto.RefundTransactionRef
            }, null, cancellationToken);
        }

        if (status == "REJECTED")
        {
            return await RejectRefundRequestAsync(id, staffUserId, new RejectRefundRequestDto
            {
                StaffNote = !string.IsNullOrWhiteSpace(dto.StaffNote) ? dto.StaffNote : "Rejected by staff."
            }, null, cancellationToken);
        }

        throw new ValidationException("Status must be either 'APPROVED' or 'REJECTED'.");
    }

    private static RefundRequestDto MapToDto(RefundRequest r)
    {
        var firstDetail = r.Order?.OrderDetails?.FirstOrDefault();

        return new RefundRequestDto
        {
            RefundRequestId = r.RefundRequestId,
            OrderId = r.OrderId,
            PaymentId = r.PaymentId,

            // Trainee snapshot info
            RequestedBy = r.RequestedBy,
            RequestedByName = r.RequestedByNavigation?.FullName ?? r.Order?.Buyer?.FullName ?? r.RequestedByNavigation?.Email,
            RequestedByEmail = r.RequestedByNavigation?.Email ?? r.Order?.Buyer?.Email,
            RequestedByAvatarUrl = r.RequestedByNavigation?.AvatarUrl ?? r.Order?.Buyer?.AvatarUrl,

            // Training package info snapshot from OrderDetails
            PackageId = firstDetail?.PackageId,
            PackageTitle = firstDetail?.PackageTitle ?? firstDetail?.Package?.Title,
            PackageDurationDays = firstDetail?.PackageDurationDays ?? firstDetail?.Package?.DurationDays,
            PackageSnapshotPrice = firstDetail?.PackagePrice,
            CoachId = r.Order?.CoachId ?? firstDetail?.Package?.CoachId,
            CoachName = r.Order?.Coach?.Coach?.FullName ?? firstDetail?.CoachName ?? firstDetail?.Package?.Coach?.Coach?.FullName,

            // Financial snapshot
            ActualPaidAmount = r.Order?.TotalAmount,
            RequestedAmount = r.RequestedAmount,
            ApprovedAmount = r.ApprovedAmount,

            // Refund details
            Reason = r.Reason,
            EvidenceUrls = ParseEvidenceUrls(r.EvidenceUrls),
            Status = r.Status,

            // Admin/Staff review
            ReviewedBy = r.ReviewedBy,
            ReviewedByName = r.ReviewedByNavigation?.FullName ?? r.ReviewedByNavigation?.Email,
            ReviewedAt = r.ReviewedAt,
            StaffNote = r.StaffNote,
            RefundTransactionRef = r.RefundTransactionRef,

            // Timestamps
            PurchaseDate = r.Order?.CreatedAt,
            RequestedAt = r.RequestedAt,
            RefundedAt = r.RefundedAt,
            CreatedAt = r.CreatedAt,

            // Terms policy snapshot
            TermId = r.TermId,
            TermVersion = r.Term?.Version,
            TermTitle = r.Term?.Title
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
                // Fall back to delimited split
            }
        }

        return evidenceUrls
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
