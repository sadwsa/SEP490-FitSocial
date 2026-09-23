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
    private readonly IPriceRepository _prices;
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAuthService _authService;

    public PaymentService(
        IUserRepository users,
        IPriceRepository prices,
        IOrderRepository orders,
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        IPaymentGateway paymentGateway,
        IAuthService authService)
    {
        _users = users;
        _prices = prices;
        _orders = orders;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _tokenService = tokenService;
        _paymentGateway = paymentGateway;
        _authService = authService;
    }

    /// <summary>
    /// Validates the coach draft BEFORE paying (model + email availability).
    /// </summary>
    public async Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (await _users.ExistsByEmailAsync(normalizedEmail))
        {
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("This email is already in use.");
        }

        var activePrice = await _prices.GetActiveAsync();

        if (activePrice == null || activePrice.Amount == null)
        {
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("Coach activation fee configuration not found.");
        }

        return ApiResponseDto<CoachActivationPreviewDto>.Ok(
            new CoachActivationPreviewDto
            {
                Email = normalizedEmail,
                AmountVnd = activePrice.Amount.Value,
                Currency = PaymentConstants.CurrencyVnd,
                OrderType = PaymentConstants.OrderTypeCoachActivation
            },
            "Order preview created. Proceed to payment.");
    }

    /// <summary>
    /// Delegates account creation to AuthService, then creates the payment order/link.
    /// </summary>
    public async Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(
        RegisterCoachRequestDto request, string originUrl, string? ipAddress = null)
    {
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

        var activePrice = await _prices.GetActiveAsync();
        if (activePrice == null || activePrice.Amount == null)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("Coach activation fee configuration not found.");
        }
        var fee = activePrice.Amount.Value;
        var now = DateTime.UtcNow;

        var orderCode = long.Parse($"{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}");

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            TraineeId = user.UserId,
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
            PackagePrice = fee,
            CreatedAt = now
        });
        order.Payments.Add(new Payment
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.OrderId,
            Amount = fee,
            Currency = PaymentConstants.CurrencyVnd,
            Method = "VietQR",
            GatewayId = 1,
            TransactionRef = $"COACH{orderCode}",
            GatewayTransactionId = orderCode.ToString(),
            Status = PaymentConstants.OrderStatusPending,
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
            return ApiResponseDto<ActivationLinkDto>.Fail("Could not save your order. Please try again.");
        }

        string frontend = string.IsNullOrWhiteSpace(originUrl) ? "https://localhost:7012" : originUrl.TrimEnd('/');
        PaymentLinkInfo link;
        string gatewayRaw = "";
        try
        {
            link = await _paymentGateway.CreatePaymentLinkAsync(
                orderCode,
                fee,
                "COACH ACTIVATION",
                $"{frontend}/payment-result",
                $"{frontend}/payment-result");
            gatewayRaw = JsonSerializer.Serialize(new { link.CheckoutUrl, link.QrCode, link.AccountNumber, link.AccountName, link.Amount, link.Description, orderCode });
        }
        catch (Exception ex)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail($"Could not create the payment link: {ex.Message}");
        }

        try
        {
            var payment = order.Payments.First();
            payment.GatewayResponseRaw = gatewayRaw;
            await _unitOfWork.SaveChangesAsync();
        }
        catch { }

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
            var existingUser = await _users.GetByIdAsync(order.TraineeId);
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

        var user = await _users.GetByIdAsync(order.TraineeId);
        if (user == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Account is not available.");
        }

        var now = DateTime.UtcNow;
        order.OrderStatus = PaymentConstants.OrderStatusPaid;
        payment.Status = PaymentConstants.PaymentStatusSuccess;
        payment.GatewayResponseRaw = JsonSerializer.Serialize(new { status.IsPaid, status.Amount, status.Reference, verifiedAt = now });
        payment.ProcessedAt = now;
        payment.UpdatedAt = now;
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
