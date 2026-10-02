using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Orders;
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
    private readonly ITrainingPackageRepository _packages;
    private readonly ICartRepository _carts;
    private readonly ITrainingPlanRepository _trainingPlans;
    private readonly INotificationRepository _notifications;
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
        IAuthService authService,
        ITrainingPackageRepository? packages = null,
        ICartRepository? carts = null,
        ITrainingPlanRepository? trainingPlans = null,
        INotificationRepository? notifications = null)
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
        _packages = packages!;
        _carts = carts!;
        _trainingPlans = trainingPlans!;
        _notifications = notifications!;
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

        if (!string.Equals(order.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This payment is not a coach activation order.");
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

        if (!string.Equals(order.OrderType, PaymentConstants.OrderTypeCoachActivation, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<bool>.Fail("This payment is not a coach activation order.");
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

    // =====================================================
    // UC-20: Purchase Training Package via PayOS VietQR
    // =====================================================

    private async Task<(User? buyer, TrainingPackage? package, string? error)> ValidatePackagePurchaseAsync(
        Guid buyerId, Guid packageId, CancellationToken ct = default)
    {
        if (_packages == null)
        {
            return (null, null, "Package purchase is not configured on the server.");
        }

        if (buyerId == Guid.Empty || packageId == Guid.Empty)
        {
            return (null, null, "Invalid buyer or package.");
        }

        var buyer = await _users.GetByIdAsync(buyerId, ct);
        if (buyer == null)
        {
            return (null, null, "Buyer account not found.");
        }

        var package = await _packages.GetByIdAsync(packageId, ct);
        if (package == null)
        {
            return (buyer, null, "Training package not found.");
        }

        // 20.0.E2 Self-Purchase Attempt
        if (package.CoachId == buyerId)
        {
            return (buyer, package, "You cannot purchase your own training package.");
        }

        // 20.0.E1 Package No Longer Active
        if (package.IsActive != true)
        {
            return (buyer, package, "This training package is no longer active or open for enrollment. Please refresh your cart.");
        }

        return (buyer, package, null);
    }

    private static long NewOrderCode()
    {
        return long.Parse($"{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}");
    }

    private static string ResolveCoachName(TrainingPackage package)
    {
        return package.Coach?.Coach?.FullName
            ?? package.Coach?.Coach?.Email
            ?? "Coach";
    }

    /// <summary>
    /// Step 2 of UC-20: build the Order Summary (package title, duration, price VND, coach, trainee).
    /// </summary>
    public async Task<ApiResponseDto<PackagePurchasePreviewDto>> PreparePackagePurchaseAsync(Guid buyerId, Guid packageId)
    {
        var (buyer, package, error) = await ValidatePackagePurchaseAsync(buyerId, packageId);
        if (error != null)
        {
            return ApiResponseDto<PackagePurchasePreviewDto>.Fail(error);
        }

        return ApiResponseDto<PackagePurchasePreviewDto>.Ok(
            new PackagePurchasePreviewDto
            {
                PackageId = package!.PackageId,
                PackageTitle = package.Title ?? "Training Package",
                DurationDays = package.DurationDays ?? 30,
                PriceVnd = package.Price ?? 0,
                Currency = PaymentConstants.CurrencyVnd,
                CoachId = package.CoachId,
                CoachName = ResolveCoachName(package),
                BuyerName = buyer!.FullName ?? buyer.Email ?? "Trainee",
                BuyerEmail = buyer.Email ?? string.Empty,
                OrderType = PaymentConstants.OrderTypePackage
            },
            "Order summary ready. Review and proceed to payment.");
    }

    /// <summary>
    /// Step 4 of UC-20: create pending order + PayOS VietQR transaction.
    /// </summary>
    public async Task<ApiResponseDto<PackagePaymentLinkDto>> CreatePackagePaymentLinkAsync(Guid buyerId, Guid packageId, string originUrl)
    {
        var (buyer, package, error) = await ValidatePackagePurchaseAsync(buyerId, packageId);
        if (error != null)
        {
            return ApiResponseDto<PackagePaymentLinkDto>.Fail(error);
        }

        var fee = package!.Price ?? 0;
        if (fee <= 0)
        {
            return ApiResponseDto<PackagePaymentLinkDto>.Fail("Invalid package price.");
        }

        // Cancel stale pending PACKAGE orders for same buyer+package (keep cart semantics)
        var staleOrders = await _orders.ListPendingActivationByUserAsync(buyerId, PaymentConstants.OrderTypePackage);
        foreach (var stale in staleOrders)
        {
            var matchesPackage = stale.OrderDetails.Any(d => d.PackageId == packageId);
            if (matchesPackage)
            {
                stale.OrderStatus = PaymentConstants.OrderStatusCancelled;
                foreach (var p in stale.Payments)
                {
                    if (p.Status == PaymentConstants.OrderStatusPending || p.Status == PaymentConstants.PaymentStatusPending)
                    {
                        p.Status = PaymentConstants.PaymentStatusFailed;
                        p.FailureReason = "Superseded by a new checkout.";
                        p.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        var now = DateTime.UtcNow;
        var orderCode = NewOrderCode();
        var coachName = ResolveCoachName(package);

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            BuyerId = buyerId,
            CoachId = package.CoachId,
            TotalAmount = fee,
            OrderStatus = PaymentConstants.OrderStatusPending,
            OrderType = PaymentConstants.OrderTypePackage,
            CreatedAt = now
        };
        order.OrderDetails.Add(new OrderDetail
        {
            OrderDetailsId = Guid.NewGuid(),
            OrderId = order.OrderId,
            PackageId = package.PackageId,
            PackagePrice = fee,
            PackageTitle = package.Title ?? "Training Package",
            PackageDurationDays = package.DurationDays ?? 30,
            CoachName = coachName,
            CreatedAt = now
        });
        order.Payments.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = fee,
            Currency = PaymentConstants.CurrencyVnd,
            Method = "VietQR",
            GatewayId = null,
            TransactionRef = $"PACKAGE{orderCode}",
            GatewayTransactionId = orderCode.ToString(),
            Status = PaymentConstants.PaymentStatusPending,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _orders.AddAsync(order);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<PackagePaymentLinkDto>.Fail("Could not save your order. Please try again.");
        }

        string frontend = string.IsNullOrWhiteSpace(originUrl) ? "https://localhost:7012" : originUrl.TrimEnd('/');
        PaymentLinkInfo link;
        try
        {
            link = await _paymentGateway.CreatePaymentLinkAsync(
                orderCode,
                fee,
                "TRAINEE PACKAGE PAYMENT",
                $"{frontend}/checkout/success",
                $"{frontend}/checkout/payment");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PackagePaymentLinkDto>.Fail($"Could not create the VietQR code: {ex.Message}");
        }

        return ApiResponseDto<PackagePaymentLinkDto>.Ok(
            new PackagePaymentLinkDto
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
            "VietQR code created. Scan with your banking app to pay.");
    }

    private static OrderDto MapPackageOrderDto(Order o)
    {
        return new OrderDto
        {
            OrderId = o.OrderId,
            BuyerId = o.BuyerId,
            BuyerName = o.Buyer?.FullName ?? o.Buyer?.Email,
            BuyerEmail = o.Buyer?.Email,
            CoachId = o.CoachId,
            CoachName = o.Coach?.Coach?.FullName ?? o.OrderDetails.FirstOrDefault()?.CoachName,
            TotalAmount = o.TotalAmount,
            OrderStatus = o.OrderStatus,
            OrderType = o.OrderType,
            CreatedAt = o.CreatedAt,
            Details = o.OrderDetails.Select(d => new OrderDetailDto
            {
                OrderDetailsId = d.OrderDetailsId,
                OrderId = d.OrderId,
                PackageId = d.PackageId,
                CoachSubscriptionPlansId = d.CoachSubscriptionPlansId,
                PackageTitle = d.PackageTitle ?? d.Package?.Title,
                PackageDurationDays = d.PackageDurationDays ?? d.Package?.DurationDays,
                CoachName = d.CoachName,
                PackagePrice = d.PackagePrice,
                CreatedAt = d.CreatedAt
            }).ToList()
        };
    }

    private static bool IsPackageOrderPaid(Order order)
    {
        var s = order.OrderStatus?.ToUpperInvariant();
        return s == PaymentConstants.OrderStatusCompleted
            || s == PaymentConstants.OrderStatusPaid
            || s == "ACTIVE";
    }

    /// <summary>
    /// Steps 6-7 of UC-20: verify PayOS, mark COMPLETED, activate enrollment + TrainingPlan, clear cart.
    /// Idempotent: already-completed orders return success without duplicating TrainingPlan.
    /// </summary>
    public async Task<ApiResponseDto<OrderDto>> CompletePackagePurchaseAsync(long orderCode)
    {
        var payment = await _payments.FindByGatewayTransactionIdAsync(orderCode.ToString());
        if (payment == null)
        {
            return ApiResponseDto<OrderDto>.Fail("Payment order not found.");
        }

        var order = await _orders.GetOrderByIdWithDetailsAsync(payment.OrderId);
        if (order == null)
        {
            return ApiResponseDto<OrderDto>.Fail("Payment order not found.");
        }

        if (!string.Equals(order.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<OrderDto>.Fail("This payment is not a training package order.");
        }

        if (IsPackageOrderPaid(order))
        {
            return ApiResponseDto<OrderDto>.Ok(MapPackageOrderDto(order), "Payment already confirmed.");
        }

        if (!string.Equals(order.OrderStatus, PaymentConstants.OrderStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<OrderDto>.Fail($"Order cannot be completed (status: {order.OrderStatus}).");
        }

        GatewayPaymentStatus status;
        try
        {
            status = await _paymentGateway.GetPaymentStatusAsync(orderCode);
        }
        catch (Exception ex)
        {
            return ApiResponseDto<OrderDto>.Fail($"Could not verify the payment: {ex.Message}");
        }

        // 20.0.E3 Payment Timeout or Failure
        if (!status.IsPaid || status.Amount < (order.TotalAmount ?? 0))
        {
            return ApiResponseDto<OrderDto>.Fail("Payment has not been completed yet. If you already transferred, wait a moment and tap Verify again.");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = PaymentConstants.OrderStatusCompleted;
        payment.Status = PaymentConstants.PaymentStatusSuccess;
        payment.ProcessedAt = now;
        payment.UpdatedAt = now;

        // POST-2: activate enrollment -> TrainingPlan linked to OrderDetails
        var detail = order.OrderDetails.FirstOrDefault();
        if (detail != null && _trainingPlans != null)
        {
            var existingPlan = await _trainingPlans.GetActiveByOrderDetailsIdAsync(detail.OrderDetailsId);
            if (existingPlan == null)
            {
                var durationDays = detail.PackageDurationDays ?? 30;
                var trainingPlan = new TrainingPlan
                {
                    TrainingPlanId = Guid.NewGuid(),
                    CoachId = order.CoachId ?? detail.Package?.CoachId ?? Guid.Empty,
                    OrderDetailsId = detail.OrderDetailsId,
                    Title = detail.PackageTitle ?? "Training Plan",
                    Description = $"Activated from package '{detail.PackageTitle}' ({durationDays} days).",
                    StartDate = now,
                    EndDate = now.AddDays(durationDays),
                    Status = "ACTIVE",
                    CreatedAt = now,
                    UpdatedAt = now,
                    PublishedAt = now
                };
                // Guard: CoachId must be valid (FK). If missing, skip plan creation but keep order completed.
                if (trainingPlan.CoachId != Guid.Empty)
                {
                    await _trainingPlans.AddAsync(trainingPlan);
                }
            }
        }

        // Remove purchased package from buyer's cart (retain other items)
        if (_carts != null && detail?.PackageId != null)
        {
            var cartItem = await _carts.GetCartItemAsync(order.BuyerId, detail.PackageId.Value);
            if (cartItem != null)
            {
                _carts.Remove(cartItem);
            }
        }

        // Notify buyer + coach (best-effort)
        if (_notifications != null)
        {
            try
            {
                var title = detail?.PackageTitle ?? "Training Package";
                await _notifications.AddAsync(new Notification
                {
                    Id = Guid.NewGuid(),
                    UserId = order.BuyerId,
                    ActorId = order.CoachId,
                    ReferenceId = order.OrderId,
                    Type = "PACKAGE_PURCHASED",
                    Description = $"Payment successful for '{title}'. Your training plan is now active.",
                    IsRead = false,
                    CreatedAt = now
                });
                if (order.CoachId.HasValue && order.CoachId.Value != Guid.Empty)
                {
                    await _notifications.AddAsync(new Notification
                    {
                        Id = Guid.NewGuid(),
                        UserId = order.CoachId.Value,
                        ActorId = order.BuyerId,
                        ReferenceId = order.OrderId,
                        Type = "PACKAGE_SOLD",
                        Description = $"New enrollment: '{title}'. Please publish the training plan.",
                        IsRead = false,
                        CreatedAt = now
                    });
                }
            }
            catch
            {
                // Notifications must never break payment fulfillment
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<OrderDto>.Fail("Could not activate the package. Please contact support with your order code.");
        }

        var completed = await _orders.GetOrderByIdWithDetailsAsync(order.OrderId);
        return ApiResponseDto<OrderDto>.Ok(
            MapPackageOrderDto(completed ?? order),
            "Payment successful! Your training package is now active.");
    }

    /// <summary>
    /// 20.1 Cancel Order Checkout: cancel pending payment, keep cart item.
    /// </summary>
    public async Task<ApiResponseDto<bool>> CancelPackagePurchaseAsync(long orderCode, Guid? requesterId = null)
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

        if (!string.Equals(order.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<bool>.Fail("This payment is not a training package order.");
        }

        if (requesterId.HasValue && requesterId.Value != Guid.Empty && order.BuyerId != requesterId.Value)
        {
            return ApiResponseDto<bool>.Fail("You do not have permission to cancel this order.");
        }

        if (!string.Equals(order.OrderStatus, PaymentConstants.OrderStatusPending, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<bool>.Fail("Only pending orders can be cancelled.");
        }

        order.OrderStatus = PaymentConstants.OrderStatusCancelled;
        payment.Status = PaymentConstants.PaymentStatusFailed;
        payment.FailureReason = "Cancelled by user at checkout.";
        payment.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponseDto<bool>.Ok(true, "Order cancelled. The package remains in your cart.");
    }
}
