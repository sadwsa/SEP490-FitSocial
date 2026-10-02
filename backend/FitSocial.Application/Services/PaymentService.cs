using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FitSocial.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUserRepository _users;
    private readonly ICoachSubscriptionPlanRepository _plans;
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly ICoachUpgradeRepository _coachUpgrades;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuthService _authService;

    public PaymentService(
        IUserRepository users,
        ICoachSubscriptionPlanRepository plans,
        IOrderRepository orders,
        IPaymentRepository payments,
        ICoachUpgradeRepository coachUpgrades,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPaymentGateway paymentGateway,
        IAuthService authService)
    {
        _users = users;
        _plans = plans;
        _orders = orders;
        _payments = payments;
        _coachUpgrades = coachUpgrades;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _paymentGateway = paymentGateway;
        _authService = authService;
    }

    private Guid? ResolvePlanId(RegisterCoachRequestDto request)
    {
        // New field first, legacy PriceId alias second
        return request.CoachSubscriptionPlansId ?? request.PriceId;
    }

    /// <summary>
    /// Validates the coach draft BEFORE paying (model + email availability).
    /// Uses selected CoachSubscriptionPlansId (required per FitConnect spec).
    /// </summary>
    public async Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (await _users.ExistsByEmailAsync(normalizedEmail))
        {
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("This email is already in use.");
        }

        var planId = ResolvePlanId(request);
        if (planId == null || planId == Guid.Empty)
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("Please select a subscription plan.");

        var plan = await _plans.GetByIdAsync(planId.Value);
        if (plan == null || plan.IsActive != true || plan.Amount == null)
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("Selected subscription plan is not available.");

        return ApiResponseDto<CoachActivationPreviewDto>.Ok(
            new CoachActivationPreviewDto
            {
                Email = normalizedEmail,
                AmountVnd = plan.Amount.Value,
                Currency = string.IsNullOrWhiteSpace(plan.Currency) ? PaymentConstants.CurrencyVnd : plan.Currency!,
                OrderType = PaymentConstants.OrderTypeCoachActivation
            },
            "Order preview created. Proceed to payment.");
    }

    /// <summary>
    /// Delegates account creation to AuthService, then creates the payment order/link.
    /// Plan is required per FitConnect spec (Confirm & Plan step).
    /// </summary>
    public async Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(
        RegisterCoachRequestDto request, string originUrl, string? ipAddress = null)
    {
        // 0. Validate plan BEFORE creating account (avoid orphan users)
        var planId = ResolvePlanId(request);
        if (planId == null || planId == Guid.Empty)
            return ApiResponseDto<ActivationLinkDto>.Fail("Please select a subscription plan.");
        var plan = await _plans.GetByIdAsync(planId.Value);
        if (plan == null || plan.IsActive != true || plan.Amount == null)
            return ApiResponseDto<ActivationLinkDto>.Fail("Selected subscription plan is not available.");

        // 1. Create the pending coach account via AuthService (handles OTP, eKYC, certificates, terms)
        var authResult = await _authService.RegisterCoachAsync(request, ipAddress);
        if (!authResult.Success || authResult.Data == null)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail(authResult.Message ?? "Could not create coach account.");
        }
        var user = authResult.Data;

        // 2. Cancel any stale pending orders for this user
        var staleOrders = await _orders.ListPendingActivationByUserAsync(user.UserId, PaymentConstants.OrderTypeCoachActivation);
        foreach (var stale in staleOrders)
        {
            stale.OrderStatus = PaymentConstants.OrderStatusCancelled;
        }

        var fee = plan.Amount.Value;
        var currency = string.IsNullOrWhiteSpace(plan.Currency) ? PaymentConstants.CurrencyVnd : plan.Currency!;
        var durationDays = plan.SubscriptionDuration ?? plan.TrainingPackageDuration ?? 365;
        var now = DateTime.UtcNow;

        var orderCode = long.Parse($"{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}");

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = user.UserId,
            CoachId = user.UserId,
            TotalAmount = fee,
            OrderStatus = PaymentConstants.OrderStatusPending,
            OrderType = PaymentConstants.OrderTypeCoachActivation,
            CreatedAt = now
        };
        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = order.OrderId,
            CoachSubscriptionPlansId = plan.CoachSubscriptionPlansId,
            PackagePrice = fee,
            PackageTitle = string.IsNullOrWhiteSpace(plan.Description) ? "Coach Subscription Plan" : plan.Description!,
            PackageDurationDays = durationDays,
            CoachName = user.FullName ?? user.Email,
            CreatedAt = now
        });
        order.Payments.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = fee,
            Currency = currency,
            Method = "VietQR",
            GatewayId = null,
            TransactionRef = $"COACH{orderCode}",
            GatewayTransactionId = orderCode.ToString(),
            Status = PaymentConstants.OrderStatusPending,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _orders.AddAsync(order);

        // Create CoachUpgrade linking the selected plan
        var upgrade = new CoachUpgrade
        {
            UpgradeId = Guid.NewGuid(),
            CoachSubscriptionPlansId = plan.CoachSubscriptionPlansId,
            CoachId = user.UserId,
            OrderId = order.OrderId,
            Status = "PENDING",
            CreatedAt = now
        };
        await _coachUpgrades.AddAsync(upgrade);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("Could not save your order. Please try again.");
        }

        string frontend = string.IsNullOrWhiteSpace(originUrl) ? "https://localhost:7012" : originUrl.TrimEnd('/');
        PaymentLinkInfo link;
        try
        {
            link = await _paymentGateway.CreatePaymentLinkAsync(
                orderCode,
                fee,
                "COACH ACTIVATION",
                $"{frontend}/payment-result",
                $"{frontend}/payment-result");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail($"Could not create the payment link: {ex.Message}");
        }

        return ApiResponseDto<ActivationLinkDto>.Ok(
            new ActivationLinkDto
            {
                CheckoutUrl = link.CheckoutUrl,
                OrderCode = orderCode,
                OrderId = order.OrderId,
                QrCode = link.QrCode,
                AccountNumber = link.AccountNumber,
                AccountName = link.AccountName,
                Amount = link.Amount,
                Description = link.Description
            },
            "Payment link created. Complete the payment to activate your coach account.");
    }

    /// <summary>
    /// Verify the PayOS payment by order code, then unlock the account and sign the coach in.
    /// Idempotent: already-paid orders simply sign the user in again.
    /// Also stores GatewayResponseRaw on success.
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> CompleteActivationAsync(long orderCode)
    {
        var payment = await _payments.FindByGatewayTransactionIdAsync(orderCode.ToString());

        if (payment == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment order not found.");
        }

        var order = await _orders.GetByIdAsync(payment.OrderId);
        if (order == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment order not found.");
        }

        if (order.OrderStatus == PaymentConstants.OrderStatusPaid)
        {
            var existingUser = await _users.GetByIdAsync(order.BuyerId);
            if (existingUser == null || existingUser.IsLocked == true)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Account is not available.");
            }
            var existingSession = await _tokenService.CreateSessionAsync(existingUser);
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Could not activate the account. Please try again.");
            }
            return ApiResponseDto<AuthResponseDto>.Ok(existingSession, "Payment already confirmed. Welcome back!");
        }

        GatewayPaymentStatus status;
        try
        {
            status = await _paymentGateway.GetPaymentStatusAsync(orderCode);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<AuthResponseDto>.Fail($"Could not verify the payment: {ex.Message}");
        }

        if (!status.IsPaid || status.Amount < (order.TotalAmount ?? 0))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment has not been completed yet.");
        }

        var user = await _users.GetByIdAsync(order.BuyerId);
        if (user == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Account is not available.");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = PaymentConstants.OrderStatusPaid;
        payment.Status = PaymentConstants.PaymentStatusSuccess;
        payment.ProcessedAt = now;
        payment.UpdatedAt = now;
        // Mark CoachUpgrade as ACTIVE and set EndDay from plan duration
        var upgrade = await _coachUpgrades.GetByOrderIdAsync(order.OrderId);
        if (upgrade != null)
        {
            upgrade.Status = "ACTIVE";
            var planForUpgrade = await _plans.GetByIdAsync(upgrade.CoachSubscriptionPlansId);
            var durationDays = planForUpgrade?.SubscriptionDuration ?? planForUpgrade?.TrainingPackageDuration ?? 365;
            upgrade.EndDay = now.AddDays(durationDays);
        }
        user.IsLocked = false;
        user.LastActiveAt = now;
        user.UpdatedAt = now;

        AuthResponseDto response;
        try
        {
            response = await _tokenService.CreateSessionAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not activate the account. Please try again.");
        }

        return ApiResponseDto<AuthResponseDto>.Ok(response, "Payment successful! Coach account created.");
    }

    public async Task<ApiResponseDto<bool>> CancelActivationAsync(long orderCode)
    {
        var payment = await _payments.FindByGatewayTransactionIdAsync(orderCode.ToString());

        if (payment == null)
        {
            return ApiResponseDto<bool>.Fail("Payment order not found.");
        }

        var order = await _orders.GetByIdAsync(payment.OrderId);
        if (order == null)
        {
            return ApiResponseDto<bool>.Fail("Payment order not found.");
        }

        if (order.OrderStatus != PaymentConstants.OrderStatusPending)
        {
            return ApiResponseDto<bool>.Fail("Only pending orders can be cancelled.");
        }

        order.OrderStatus = PaymentConstants.OrderStatusCancelled;
        payment.Status = PaymentConstants.PaymentStatusFailed;
        payment.FailureReason = "Cancelled by user.";
        payment.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponseDto<bool>.Ok(true, "Payment order cancelled.");
    }
}
