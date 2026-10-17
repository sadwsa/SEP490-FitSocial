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
    private readonly IAuditLogService? _auditLogService;
    private readonly ICoachBankAccountRepository? _bankAccountRepository;
    private readonly IEncryptionService? _encryptionService;
    private readonly IBankTransferGateway? _bankTransferGateway;
    private readonly IEmailService? _emailService;

    public RefundRequestService(
        IRefundRequestRepository refundRequestRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IAuditLogRepository? auditLogs = null,
        INotificationRepository? notifications = null,
        ITrainingPlanRepository? trainingPlans = null,
        ITermsAndPolicyRepository? terms = null,
        IUserRepository? users = null,
        IChatRealtimeNotifier? realtimeNotifier = null,
        IAuditLogService? auditLogService = null,
        ICoachBankAccountRepository? bankAccountRepository = null,
        IEncryptionService? encryptionService = null,
        IBankTransferGateway? bankTransferGateway = null,
        IEmailService? emailService = null)
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
        _auditLogService = auditLogService;
        _bankAccountRepository = bankAccountRepository;
        _encryptionService = encryptionService;
        _bankTransferGateway = bankTransferGateway;
        _emailService = emailService;
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

    private static string MaskAccountNumber(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return "—";
        var trimmed = account.Trim();
        if (trimmed.Length <= 4) return "****";
        var prefix = trimmed.Length > 8 ? trimmed[..4] : trimmed[..2];
        var suffix = trimmed[^4..];
        return $"{prefix}******{suffix}";
    }

    /// <summary>
    /// Core engine implementing the 3-case Refund Policy:
    /// - Case 1: Within 7 days, 0 completed/no-show sessions AND training plan not sent -> 100% Trainee refund, 0 Coach payout.
    /// - Case 2: Within 7 days, 1-2 sessions attended OR coach created/drafted curriculum -> Deduct coach effort, Trainee gets remainder.
    /// - Case 3: Mid-term / Coach late schedule (rules require plan at least 2 days before session) -> Pro-rata for unperformed sessions.
    /// </summary>
    public async Task<RefundPolicyCalculationDto> EvaluateRefundPolicyCoreAsync(
        Order order,
        string? reason = null,
        Guid? preferredBankAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var firstDetail = order.OrderDetails?.FirstOrDefault();
        var package = firstDetail?.Package;
        var totalAmount = order.TotalAmount ?? firstDetail?.PackagePrice ?? 0;

        // 1. Determine Total Sessions
        int totalSessions = package?.SessionCount ?? 0;
        int durationDays = firstDetail?.PackageDurationDays ?? package?.DurationDays ?? 30;

        TrainingPlan? plan = null;
        if (_trainingPlans != null && firstDetail != null)
        {
            try
            {
                plan = await _trainingPlans.GetTraineePlanWithLogsAsync(firstDetail.OrderDetailsId, order.BuyerId, cancellationToken)
                       ?? await _trainingPlans.GetActiveByOrderDetailsIdAsync(firstDetail.OrderDetailsId, cancellationToken);
            }
            catch
            {
                // Fallback if query fails
            }
        }

        if (totalSessions <= 0)
        {
            if (plan?.TrainingPlanExercises != null && plan.TrainingPlanExercises.Any())
            {
                totalSessions = plan.TrainingPlanExercises
                    .Select(e => (e.WeekNumber ?? 1, e.DayNumber ?? 1))
                    .Distinct()
                    .Count();
            }
            else
            {
                totalSessions = Math.Max(1, durationDays / 2);
            }
        }
        totalSessions = Math.Max(1, totalSessions);

        // 2. Determine Completed / Attended Sessions
        int completedSessions = 0;
        if (plan?.TrainingPlanExercises != null && plan.TrainingPlanExercises.Any())
        {
            completedSessions = plan.TrainingPlanExercises
                .Where(e => e.WorkoutCompletionLogs != null && e.WorkoutCompletionLogs.Any(l =>
                    l.TraineeId == order.BuyerId &&
                    (string.Equals(l.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(l.Status, "TRAINEE_NO_SHOW", StringComparison.OrdinalIgnoreCase))))
                .Select(e => (e.WeekNumber ?? 1, e.DayNumber ?? 1))
                .Distinct()
                .Count();
        }
        completedSessions = Math.Min(totalSessions, completedSessions);
        int unperformedSessions = Math.Max(0, totalSessions - completedSessions);

        // 3. Determine Plan Sent and Plan Created status
        bool isPlanSent = plan != null && (plan.PublishedAt.HasValue ||
            string.Equals(plan.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(plan.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase));

        bool isPlanCreated = isPlanSent || (plan != null && plan.TrainingPlanExercises != null && plan.TrainingPlanExercises.Any());

        // 4. Check 7-day policy window
        var purchaseTime = order.Payments?.FirstOrDefault(p =>
                               p.Status == PaymentConstants.PaymentStatusSuccess
                               || p.Status == PaymentConstants.OrderStatusPaid
                               || p.Status == PaymentConstants.OrderStatusCompleted)?.ProcessedAt
                           ?? order.CreatedAt ?? DateTime.UtcNow;
        var daysSincePurchase = (DateTime.UtcNow - purchaseTime).TotalDays;
        bool isWithin7Days = daysSincePurchase <= 7.0;

        // 5. Check late schedule violation flag
        var reasonLower = (reason ?? string.Empty).ToLowerInvariant();
        bool isLateScheduleViolation = reasonLower.Contains("trễ lịch") ||
                                       reasonLower.Contains("trễ giáo án") ||
                                       reasonLower.Contains("muộn lịch") ||
                                       reasonLower.Contains("chậm lịch") ||
                                       reasonLower.Contains("chậm giáo án") ||
                                       reasonLower.Contains("late plan") ||
                                       reasonLower.Contains("late schedule") ||
                                       reasonLower.Contains("coach late") ||
                                       reasonLower.Contains("chưa có lịch") ||
                                       reasonLower.Contains("không có lịch") ||
                                       reasonLower.Contains("vi phạm lịch");

        decimal unitPrice = totalSessions > 0 ? Math.Round(totalAmount / totalSessions, 0) : totalAmount;
        string refundCase;
        string caseTitle;
        string caseDescription;
        decimal traineeRefund;
        decimal coachPayout;

        if (isWithin7Days && completedSessions == 0 && !isPlanSent)
        {
            // Case 1: Within the first 7 days, no sessions attended AND no training plan sent
            refundCase = "CASE_1";
            caseTitle = "Case 1: Within first 7 days, no sessions attended & no training plan sent";
            caseDescription = "The system automatically calculates a 100% tuition refund for the trainee. The coach receives no payout as no sessions were held and no training plan was submitted.";
            traineeRefund = totalAmount;
            coachPayout = 0;
        }
        else if (isWithin7Days && (completedSessions > 0 || isPlanCreated))
        {
            // Case 2: Within the first 7 days, 1-2 sessions attended OR coach drafted training plan
            refundCase = "CASE_2";
            caseTitle = "Case 2: Within first 7 days, 1-2 sessions attended or coach drafted training plan";
            caseDescription = "Deducts coach effort compensation (completed sessions or curriculum design effort). The remaining balance is refunded to the trainee.";

            if (completedSessions > 0)
            {
                coachPayout = Math.Min(totalAmount, unitPrice * completedSessions);
            }
            else
            {
                // Coach drafted training plan but trainee hasn't attended any session -> compensate 1 session effort
                coachPayout = Math.Min(totalAmount, unitPrice * 1);
            }
            traineeRefund = Math.Max(0, totalAmount - coachPayout);
        }
        else
        {
            // Case 3: Mid-term violation / Coach late with training schedule (2-day advance rule)
            refundCase = "CASE_3";
            caseTitle = "Case 3: Mid-term violation / Coach late with training schedule (2-day advance rule)";
            caseDescription = "Applies pro-rata refund for all unperformed sessions. The coach is only paid for sessions marked Completed.";

            traineeRefund = Math.Min(totalAmount, Math.Round(unitPrice * unperformedSessions, 0));
            coachPayout = Math.Min(totalAmount - traineeRefund, Math.Round(unitPrice * completedSessions, 0));
        }

        // Payout Bank Account Target lookup
        CoachBankAccount? targetBank = null;
        string? maskedAccount = null;
        if (_bankAccountRepository != null)
        {
            try
            {
                var banks = await _bankAccountRepository.ListActiveByCoachIdAsync(order.BuyerId, cancellationToken);
                if (preferredBankAccountId.HasValue)
                {
                    targetBank = banks.FirstOrDefault(b => b.BankId == preferredBankAccountId.Value);
                }
                targetBank ??= banks.FirstOrDefault(b => b.IsDefault == true) ?? banks.FirstOrDefault();

                if (targetBank != null && _encryptionService != null)
                {
                    var plain = _encryptionService.Decrypt(targetBank.EncryptedAccountNumber);
                    maskedAccount = MaskAccountNumber(plain);
                }
            }
            catch
            {
                // Non-fatal
            }
        }

        return new RefundPolicyCalculationDto
        {
            RefundCase = refundCase,
            CaseTitle = caseTitle,
            CaseDescription = caseDescription,
            IsWithin7Days = isWithin7Days,
            TotalSessions = totalSessions,
            CompletedSessions = completedSessions,
            UnperformedSessions = unperformedSessions,
            UnitPricePerSession = unitPrice,
            TotalPaidAmount = totalAmount,
            TraineeRefundAmount = traineeRefund,
            CoachPayoutAmount = coachPayout,
            IsPlanSent = isPlanSent,
            IsPlanCreated = isPlanCreated,
            IsLateScheduleViolation = isLateScheduleViolation,
            TargetBankAccountId = targetBank?.BankId,
            TargetBankName = targetBank?.BankName,
            TargetBankCode = targetBank?.BankCode,
            TargetAccountName = targetBank?.AccountName,
            TargetAccountNumberMasked = maskedAccount
        };
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

        // Trainee must have an active bank account before creating refund request
        if (_bankAccountRepository != null)
        {
            var activeBanks = await _bankAccountRepository.ListActiveByCoachIdAsync(userId, cancellationToken);
            if (activeBanks == null || !activeBanks.Any())
            {
                throw new BusinessException("Bạn chưa liên kết tài khoản ngân hàng để nhận tiền hoàn. Vui lòng thêm tài khoản ngân hàng trước khi tạo yêu cầu hoàn tiền.");
            }
        }

        // Evaluate 3-case policy breakdown
        var calc = await EvaluateRefundPolicyCoreAsync(order, dto.Reason, dto.BankAccountId, cancellationToken);

        if (!calc.IsWithin7Days)
        {
            if (calc.UnperformedSessions <= 0)
            {
                throw new BusinessException("The package has already completed all sessions and is no longer eligible for refund.");
            }

            if (!calc.IsLateScheduleViolation)
            {
                throw new BusinessException("The refund request period has expired (refunds are only permitted within 7 days of purchase or for mid-term coach schedule violations).");
            }
        }

        // Coach late schedule violation requires proof (image evidence)
        if (calc.IsLateScheduleViolation)
        {
            if (dto.EvidenceUrls == null || !dto.EvidenceUrls.Any(u => !string.IsNullOrWhiteSpace(u)))
            {
                throw new ValidationException("Bằng chứng hình ảnh là bắt buộc đối với trường hợp khiếu nại Coach lên trễ lịch.");
            }
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
        var purchaseTime = order.Payments?.FirstOrDefault(p =>
                               p.Status == PaymentConstants.PaymentStatusSuccess
                               || p.Status == PaymentConstants.OrderStatusPaid
                               || p.Status == PaymentConstants.OrderStatusCompleted)?.ProcessedAt
                           ?? order.CreatedAt ?? now;

        // Auto-calculated refund amount adhering strictly to policy cases
        var snapshotPaidAmount = calc.TraineeRefundAmount > 0 ? calc.TraineeRefundAmount : (order.TotalAmount ?? order.OrderDetails.FirstOrDefault()?.PackagePrice ?? 0);

        var policySnapshotNote = $"[Policy: {calc.RefundCase} | Sessions: {calc.CompletedSessions}/{calc.TotalSessions} | Trainee Refund: {calc.TraineeRefundAmount:N0} VND | Coach Payout: {calc.CoachPayoutAmount:N0} VND{(calc.TargetBankName != null ? $" | Bank: {calc.TargetBankName} - {calc.TargetAccountName} ({calc.TargetAccountNumberMasked})" : "")}]";

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
            StaffNote = policySnapshotNote,
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Transactional execution for refund creation, dual notification, and audit logging
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _refundRequestRepository.AddAsync(refundRequest, cancellationToken);

            // Snapshot into order: mark order as REFUND_PENDING
            order.OrderStatus = PaymentConstants.OrderStatusRefundPending;

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
            var auditDesc = $"Refund request created.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOriginal Paid Amount: {snapshotPaidAmount:N0} VND\nPurchase Date: {purchaseTime:dd/MM/yyyy HH:mm}\nRequested At: {now:dd/MM/yyyy HH:mm}\nStatus: PENDING\nReason: {refundRequest.Reason}";
            if (_auditLogService != null)
            {
                await _auditLogService.LogAsync(
                    actorAccountId: userId,
                    action: "CREATE_REFUND_REQUEST",
                    entityType: "RefundRequest",
                    entityId: refundRequest.RefundRequestId.ToString(),
                    description: auditDesc,
                    oldValue: null,
                    newValue: new
                    {
                        buyer = buyerName,
                        seller = coachName,
                        package = packageName,
                        paidAmount = snapshotPaidAmount,
                        purchaseDate = purchaseTime,
                        requestedAt = now,
                        status = "PENDING",
                        reason = refundRequest.Reason
                    },
                    cancellationToken: cancellationToken);
            }
            else if (_auditLogs != null)
            {
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
                        purchaseDate = purchaseTime,
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
            var createdDto = MapToDto(created);
            await EnrichDtoWithPolicyAsync(created, createdDto, cancellationToken);
            return ApiResponseDto<RefundRequestDto>.Ok(createdDto, "Refund request submitted successfully.");
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
        var dtos = new List<RefundRequestDto>();
        foreach (var item in list)
        {
            var dto = MapToDto(item);
            await EnrichDtoWithPolicyAsync(item, dto, cancellationToken);
            dtos.Add(dto);
        }
        return ApiResponseDto<List<RefundRequestDto>>.Ok(dtos, "My refund requests retrieved.");
    }

    public async Task<ApiResponseDto<List<RefundRequestDto>>> GetAllRefundRequestsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _refundRequestRepository.ListAllWithDetailsAsync(status, cancellationToken);
        var dtos = new List<RefundRequestDto>();
        foreach (var item in list)
        {
            var dto = MapToDto(item);
            await EnrichDtoWithPolicyAsync(item, dto, cancellationToken);
            dtos.Add(dto);
        }
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

        var dtos = new List<RefundRequestDto>();
        foreach (var item in items)
        {
            var dto = MapToDto(item);
            await EnrichDtoWithPolicyAsync(item, dto, cancellationToken);
            dtos.Add(dto);
        }

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

        var dto = MapToDto(item);
        await EnrichDtoWithPolicyAsync(item, dto, cancellationToken);
        return ApiResponseDto<RefundRequestDto>.Ok(dto);
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

        // LEVEL 1 Concurrency Snapshot: Validation and gateway execution
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

            // Evaluate policy to determine refund breakdown
            var calc = await EvaluateRefundPolicyCoreAsync(order, item.Reason, dto?.BankAccountId, cancellationToken);

            // Financial snapshot amount: Stored at transaction time or policy calculated
            var snapshotAmount = item.RequestedAmount ?? order.TotalAmount ?? 0;
            var approvedAmount = dto?.ApprovedAmount is > 0
                ? dto.ApprovedAmount.Value
                : (calc.TraineeRefundAmount > 0 ? calc.TraineeRefundAmount : snapshotAmount);

            if (approvedAmount > (order.TotalAmount ?? snapshotAmount))
            {
                throw new ValidationException($"Approved amount ({approvedAmount:N0} VND) cannot exceed the original paid amount ({order.TotalAmount:N0} VND).");
            }

            // Payout Bank Account for Trainee
            CoachBankAccount? bank = null;
            string plainAccountNumber = string.Empty;
            if (_bankAccountRepository != null)
            {
                var targetBankId = dto?.BankAccountId ?? calc.TargetBankAccountId;
                var banks = await _bankAccountRepository.ListActiveByCoachIdAsync(item.RequestedBy, cancellationToken);
                bank = (targetBankId.HasValue ? banks.FirstOrDefault(b => b.BankId == targetBankId.Value) : null)
                       ?? banks.FirstOrDefault(b => b.IsDefault == true)
                       ?? banks.FirstOrDefault();

                if (bank == null)
                {
                    throw new BusinessException("The trainee has not configured a payout bank account. Please ask the trainee to add a bank account under Payout Account before approving the refund.");
                }

                if (_encryptionService != null)
                {
                    plainAccountNumber = _encryptionService.Decrypt(bank.EncryptedAccountNumber) ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(plainAccountNumber))
                {
                    throw new BusinessException("Unable to decrypt the trainee's bank account number.");
                }
            }

            // Idempotent refund transaction reference
            var txRef = !string.IsNullOrWhiteSpace(dto?.RefundTransactionRef)
                ? dto.RefundTransactionRef.Trim()
                : (!string.IsNullOrWhiteSpace(item.RefundTransactionRef)
                    ? item.RefundTransactionRef
                    : $"RF{item.RefundRequestId:N}"[..18].ToUpperInvariant());

            // Automated Disbursement via Banking Gateway (PayOS Napas 24/7)
            if (_bankTransferGateway != null && bank != null && approvedAmount > 0)
            {
                var transferReq = new BankTransferRequest(
                    BankCode: bank.BankCode ?? string.Empty,
                    AccountNumber: plainAccountNumber,
                    AccountName: bank.AccountName ?? string.Empty,
                    Amount: approvedAmount,
                    Currency: "VND",
                    Description: $"REFUND ORD {order.OrderId.ToString("N")[..8].ToUpperInvariant()}",
                    Reference: txRef);

                CreatedTransfer transferResult;
                try
                {
                    transferResult = await _bankTransferGateway.CreateTransferAsync(transferReq, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw new BusinessException($"Banking gateway connection error during refund disbursement: {ex.Message}. The refund request was not approved.");
                }

                if (!transferResult.Created || (!transferResult.Succeeded && !string.IsNullOrWhiteSpace(transferResult.FailureReason)))
                {
                    throw new BusinessException($"Bank transfer refund failed: {transferResult.FailureReason ?? "Transaction was not successful"}. The refund request remains in PENDING status.");
                }

                if (!string.IsNullOrWhiteSpace(transferResult.TransactionRef))
                {
                    txRef = transferResult.TransactionRef;
                }
                else if (!string.IsNullOrWhiteSpace(transferResult.GatewayRef))
                {
                    txRef = transferResult.GatewayRef;
                }
            }

            var now = DateTime.UtcNow;

            var staffNoteText = dto?.StaffNote?.Trim();
            var bankTransferSummary = bank != null
                ? $"[Automatic bank transfer of {approvedAmount:N0} VND to {bank.BankName} - {bank.AccountName} ({MaskAccountNumber(plainAccountNumber)}) - Ref: {txRef}]"
                : null;
            var finalStaffNote = string.IsNullOrWhiteSpace(staffNoteText)
                ? bankTransferSummary
                : (bankTransferSummary != null ? $"{staffNoteText}\n{bankTransferSummary}" : staffNoteText);

            // Atomic database state mutation
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                // Update RefundRequest entity (Concurrency token on Status property ensures atomic transition)
                item.Status = "APPROVED";
                item.ApprovedAmount = approvedAmount;
                item.RefundTransactionRef = txRef;
                item.ReviewedBy = staffUserId;
                item.ReviewedAt = now;
                item.RefundedAt = now;
                item.StaffNote = finalStaffNote;
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
                var buyerBankMsg = bank != null ? $" The funds have been transferred to your {bank.BankName} account ({MaskAccountNumber(plainAccountNumber)})." : "";
                var buyerMsg = $"Your refund request for \"{packageName}\" has been approved. The refund amount is {approvedAmount:N0} VND.{buyerBankMsg}";
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
                var coachEarningMsg = calc.CoachPayoutAmount > 0
                    ? $" You have been disbursed {calc.CoachPayoutAmount:N0} VND for completed sessions/curriculum design effort."
                    : "";
                if (coachId.HasValue && coachId.Value != Guid.Empty && coachId.Value != item.RequestedBy)
                {
                    var coachMsg = $"A refund request for your training package \"{packageName}\" has been approved. The refund amount is {approvedAmount:N0} VND.{coachEarningMsg}";
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
                var auditDesc = $"Refund request approved & disbursed.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOrder: {orderRef}\nOriginal Paid Amount: {snapshotAmount:N0} VND\nRefund Amount: {approvedAmount:N0} VND\nCoach Payout Portion: {calc.CoachPayoutAmount:N0} VND\nPolicy Case: {calc.RefundCase} ({calc.CaseTitle})\nTarget Bank: {(bank != null ? $"{bank.BankName} - {bank.AccountName} ({MaskAccountNumber(plainAccountNumber)})" : "None")}\nTx Ref: {txRef}\nPurchase Date: {(order.CreatedAt.HasValue ? order.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—")}\nProcessed At: {now:dd/MM/yyyy HH:mm}\nProcessed By: {staffName}";
                if (_auditLogService != null)
                {
                    await _auditLogService.LogAsync(
                        actorAccountId: staffUserId,
                        action: "APPROVE_REFUND",
                        entityType: "RefundRequest",
                        entityId: item.RefundRequestId.ToString(),
                        description: auditDesc,
                        oldValue: new { status = currentStatus, requestedAmount = item.RequestedAmount },
                        newValue: new
                        {
                            buyer = buyerName,
                            seller = coachName,
                            package = packageName,
                            order = orderRef,
                            originalPaidAmount = snapshotAmount,
                            refundAmount = approvedAmount,
                            coachPayoutPortion = calc.CoachPayoutAmount,
                            policyCase = calc.RefundCase,
                            bankAccount = bank != null ? $"{bank.BankName} ({MaskAccountNumber(plainAccountNumber)})" : null,
                            status = "APPROVED",
                            approvedAmount,
                            refundTransactionRef = txRef,
                            staffNote = item.StaffNote,
                            processedBy = staffName,
                            processedAt = now
                        },
                        ipAddress: ipAddress,
                        cancellationToken: cancellationToken);
                }
                else if (_auditLogs != null)
                {
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
                            coachPayoutPortion = calc.CoachPayoutAmount,
                            policyCase = calc.RefundCase,
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

                // 5. Send email notification asynchronously in the background so staff approval responds immediately
                if (_emailService != null)
                {
                    var emailData = PrepareRefundEmail(item, order, approvedAmount, txRef, bank, plainAccountNumber, calc, staffName);
                    if (!string.IsNullOrWhiteSpace(emailData.Email))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendEmailAsync(emailData.Email, emailData.Subject, emailData.Body);
                            }
                            catch
                            {
                                // Non-fatal background email delivery
                            }
                        });
                    }
                }

                var resultDto = MapToDto(item);
                ApplyPolicyToDto(resultDto, calc);
                return ApiResponseDto<RefundRequestDto>.Ok(resultDto, "Refund request approved and processed successfully.");
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
            if (order != null && string.Equals(order.OrderStatus, PaymentConstants.OrderStatusRefundPending, StringComparison.OrdinalIgnoreCase))
            {
                order.OrderStatus = PaymentConstants.OrderStatusCompleted;
            }
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
            var auditDesc = $"Refund request rejected.\nBuyer: {buyerName}\nSeller: {coachName}\nTraining Package: {packageName}\nOrder: {orderRef}\nOriginal Paid Amount: {snapshotAmount:N0} VND\nPurchase Date: {(order?.CreatedAt.HasValue == true ? order.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "—")}\nRequested At: {(item.RequestedAt.HasValue ? item.RequestedAt.Value.ToString("dd/MM/yyyy HH:mm") : now.ToString("dd/MM/yyyy HH:mm"))}\nProcessed At: {now:dd/MM/yyyy HH:mm}\nPrevious Status: PENDING\nNew Status: REJECTED\nProcessed By: {staffName}\nRejection Reason: {rejectionReason}";
            if (_auditLogService != null)
            {
                await _auditLogService.LogAsync(
                    actorAccountId: staffUserId,
                    action: "REJECT_REFUND",
                    entityType: "RefundRequest",
                    entityId: item.RefundRequestId.ToString(),
                    description: auditDesc,
                    oldValue: new { status = currentStatus },
                    newValue: new
                    {
                        buyer = buyerName,
                        seller = coachName,
                        package = packageName,
                        order = orderRef,
                        status = "REJECTED",
                        staffNote = item.StaffNote,
                        processedBy = staffName,
                        rejectionReason,
                        processedAt = now
                    },
                    ipAddress: ipAddress,
                    cancellationToken: cancellationToken);
            }
            else if (_auditLogs != null)
            {
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
                BankAccountId = dto.BankAccountId,
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

    public async Task<ApiResponseDto<RefundPolicyCalculationDto>> CalculatePolicyForOrderAsync(
        Guid orderId,
        string? reason = null,
        Guid? preferredBankAccountId = null,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetOrderByIdWithDetailsAsync(orderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException("Order not found.");
        }

        var calc = await EvaluateRefundPolicyCoreAsync(order, reason, preferredBankAccountId, cancellationToken);
        return ApiResponseDto<RefundPolicyCalculationDto>.Ok(calc, "Refund policy calculation completed.");
    }

    public async Task<ApiResponseDto<RefundPolicyCalculationDto>> CalculatePolicyForRefundRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _refundRequestRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (item == null)
        {
            throw new NotFoundException("Refund request not found.");
        }

        var order = item.Order ?? await _orderRepository.GetOrderByIdWithDetailsAsync(item.OrderId, cancellationToken);
        if (order == null)
        {
            throw new NotFoundException("Associated order not found.");
        }

        var calc = await EvaluateRefundPolicyCoreAsync(order, item.Reason, null, cancellationToken);
        return ApiResponseDto<RefundPolicyCalculationDto>.Ok(calc, "Refund policy calculation completed.");
    }

    private static void ApplyPolicyToDto(RefundRequestDto dto, RefundPolicyCalculationDto calc)
    {
        dto.RefundCase = calc.RefundCase;
        dto.RefundCaseTitle = calc.CaseTitle;
        dto.RefundCaseDescription = calc.CaseDescription;
        dto.TotalSessions = calc.TotalSessions;
        dto.CompletedSessions = calc.CompletedSessions;
        dto.UnperformedSessions = calc.UnperformedSessions;
        dto.UnitPricePerSession = calc.UnitPricePerSession;
        dto.TraineeRefundAmount = calc.TraineeRefundAmount;
        dto.CoachPayoutAmount = calc.CoachPayoutAmount;
        dto.IsPlanCreated = calc.IsPlanCreated;
        dto.IsPlanSent = calc.IsPlanSent;
        dto.IsWithin7Days = calc.IsWithin7Days;
        dto.IsLateScheduleViolation = calc.IsLateScheduleViolation;
        dto.BankAccountId = calc.TargetBankAccountId;
        dto.BankName = calc.TargetBankName;
        dto.BankCode = calc.TargetBankCode;
        dto.AccountName = calc.TargetAccountName;
        dto.MaskedAccountNumber = calc.TargetAccountNumberMasked;
    }

    private async Task EnrichDtoWithPolicyAsync(RefundRequest r, RefundRequestDto dto, CancellationToken ct)
    {
        try
        {
            var order = r.Order ?? await _orderRepository.GetOrderByIdWithDetailsAsync(r.OrderId, ct);
            if (order != null)
            {
                var calc = await EvaluateRefundPolicyCoreAsync(order, r.Reason, null, ct);
                ApplyPolicyToDto(dto, calc);
            }
        }
        catch
        {
            // Non-fatal if calculation enrichment fails for display
        }
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

    private static (string Email, string Subject, string Body) PrepareRefundEmail(
        RefundRequest item,
        Order order,
        decimal amount,
        string transactionRef,
        CoachBankAccount? bank,
        string plainAccountNumber,
        RefundPolicyCalculationDto calc,
        string staffName)
    {
        var traineeEmail = item.RequestedByNavigation?.Email ?? order.Buyer?.Email;
        if (string.IsNullOrWhiteSpace(traineeEmail))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        var traineeName = item.RequestedByNavigation?.FullName ?? order.Buyer?.FullName ?? traineeEmail;
        var firstDetail = order.OrderDetails?.FirstOrDefault();
        var packageName = firstDetail?.PackageTitle ?? firstDetail?.Package?.Title ?? "Training Package";
        var orderRef = $"#ORD-{order.OrderId.ToString("N")[..8].ToUpperInvariant()}";
        var bankInfo = bank != null
            ? $"{bank.BankName} - {bank.AccountName} ({MaskAccountNumber(plainAccountNumber)})"
            : "Linked Payout Bank Account";

        var subject = $"[FitSocial] Refund Confirmation: {amount:N0} VND - {orderRef}";
        var body = $@"
        <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;"">
            <div style=""text-align: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 2px solid #FF5722;"">
                <h2 style=""color: #FF5722; margin: 0; font-size: 24px; font-weight: bold;"">FitSocial</h2>
                <p style=""color: #666; margin: 6px 0 0; font-size: 14px;"">Online Tuition Refund Notification (PayOS Napas 24/7)</p>
            </div>

            <p style=""color: #333; font-size: 16px; margin-bottom: 16px;"">Hello <strong>{System.Net.WebUtility.HtmlEncode(traineeName)}</strong>,</p>
            <p style=""color: #555; line-height: 1.6; margin-bottom: 20px;"">
                Your refund request for order <strong>{System.Net.WebUtility.HtmlEncode(orderRef)}</strong> has been approved by customer support, and the system has processed a direct bank transfer payout.
            </p>

            <div style=""background: #fff8f5; border: 1px solid #ffccbc; border-radius: 6px; padding: 16px; margin-bottom: 20px;"">
                <h3 style=""color: #d84315; margin: 0 0 12px 0; font-size: 16px;"">Refund Transaction Breakdown</h3>
                <table style=""width: 100%; border-collapse: collapse; font-size: 14px;"">
                    <tr>
                        <td style=""padding: 6px 0; color: #777; width: 40%;"">Training Package:</td>
                        <td style=""padding: 6px 0; color: #333; font-weight: 600;"">{System.Net.WebUtility.HtmlEncode(packageName)}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Order Reference:</td>
                        <td style=""padding: 6px 0; color: #333; font-weight: 600;"">{System.Net.WebUtility.HtmlEncode(orderRef)}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Refund Amount:</td>
                        <td style=""padding: 6px 0; color: #2e7d32; font-size: 18px; font-weight: bold;"">{amount:N0} VND</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Receiving Bank Account:</td>
                        <td style=""padding: 6px 0; color: #333; font-weight: 600;"">{System.Net.WebUtility.HtmlEncode(bankInfo)}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Transaction Reference:</td>
                        <td style=""padding: 6px 0; color: #333; font-family: monospace; font-size: 13px;"">{System.Net.WebUtility.HtmlEncode(transactionRef)}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Policy Applied:</td>
                        <td style=""padding: 6px 0; color: #333;"">{System.Net.WebUtility.HtmlEncode(calc.CaseTitle ?? "FitSocial Refund Policy")}</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Processed Time:</td>
                        <td style=""padding: 6px 0; color: #333;"">{DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm:ss} (GMT+7)</td>
                    </tr>
                    <tr>
                        <td style=""padding: 6px 0; color: #777;"">Reviewed By:</td>
                        <td style=""padding: 6px 0; color: #333;"">{System.Net.WebUtility.HtmlEncode(staffName)}</td>
                    </tr>
                </table>
            </div>

            <p style=""color: #555; line-height: 1.6; margin-bottom: 16px;"">
                Funds have been transferred via the Napas 24/7 interbank payment gateway. Please check your account balance in your banking application.
            </p>

            <hr style=""border: none; border-top: 1px solid #eeeeee; margin: 24px 0;"" />
            <p style=""color: #888; font-size: 12px; text-align: center; margin: 0; line-height: 1.5;"">
                If you have any questions or require further assistance, please contact FitSocial support via email or hotline.<br />
                © {DateTime.UtcNow.Year} FitSocial Platform. All rights reserved.
            </p>
        </div>";

        return (traineeEmail, subject, body);
    }
}

