using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IUserRepository _users;
    private readonly ISportRepository _sports;
    private readonly IPriceRepository _prices;
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly ICoachProfileRepository _coachProfiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IPaymentGateway _paymentGateway;

    public PaymentService(
        IUserRepository users,
        ISportRepository sports,
        IPriceRepository prices,
        IOrderRepository orders,
        IPaymentRepository payments,
        ICoachProfileRepository coachProfiles,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IPaymentGateway paymentGateway)
    {
        _users = users;
        _sports = sports;
        _prices = prices;
        _orders = orders;
        _payments = payments;
        _coachProfiles = coachProfiles;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _paymentGateway = paymentGateway;
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
    /// Verify OTP + create a LOCKED coach account, a PENDING order and a PayOS payment link.
    /// The account is unlocked only after the payment is verified (complete call or webhook).
    /// Retrying with the same email reuses the locked account and cancels its old pending orders.
    /// </summary>
    public async Task<ApiResponseDto<ActivationLinkDto>> CreateActivationLinkAsync(
        RegisterCoachRequestDto request, string originUrl)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.Password != request.ConfirmPassword)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("Passwords do not match.");
        }

        var existing = await _users.FindByEmailAsync(normalizedEmail);

        if (existing != null && existing.IsLocked != true)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("This email is already in use.");
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (normalizedPhone != null &&
            await _users.ExistsByPhoneAsync(normalizedPhone, existing?.UserId))
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("This phone number is already in use.");
        }

        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpConstants.PurposeRegisterCoach);

        if (!otpValid)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail(otpMessage);
        }

        var activePrice = await _prices.GetActiveAsync();

        if (activePrice == null || activePrice.Amount == null)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("Coach activation fee configuration not found.");
        }

        var fee = activePrice.Amount.Value;
        var now = DateTime.UtcNow;

        User user;
        CoachProfile? profile;
        if (existing == null)
        {
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = request.FullName.Trim(),
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
                DateOfBirth = request.DateOfBirth,
                RoleCode = RoleConstants.Coach,
                IsInternal = false,
                IsLocked = true, 
                CreatedAt = now,
                UpdatedAt = now,
                CoachProfileCoach = new CoachProfile
                {
                    ApprovalStatus = "PENDING",
                    UpdatedAt = now
                }
            };
            user.CoachProfileCoach.CoachId = user.UserId;
            await _users.AddAsync(user);
            profile = user.CoachProfileCoach;
        }
        else
        {
            user = existing;
            user.FullName = request.FullName.Trim();
            user.PasswordHash = _passwordHasher.HashPassword(request.Password);
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            user.UpdatedAt = now;

            var staleOrders = await _orders.ListPendingActivationByUserAsync(
                user.UserId, PaymentConstants.OrderTypeCoachActivation);
            foreach (var stale in staleOrders)
            {
                stale.OrderStatus = PaymentConstants.OrderStatusCancelled;
            }

            profile = await _coachProfiles.FindWithSportsByCoachIdAsync(user.UserId);
            if (profile == null)
            {
                profile = new CoachProfile { CoachId = user.UserId };
                await _coachProfiles.AddAsync(profile);
            }
        }

        profile.ApprovalStatus = "PENDING";
        profile.ExperienceYears = request.ExperienceYears;
        profile.Bio = string.IsNullOrWhiteSpace(request.Biography) ? null : request.Biography.Trim();
        profile.CertificateUrl = string.IsNullOrWhiteSpace(request.CertificateUrl) ? null : request.CertificateUrl.Trim();
        profile.IdentityCardUrl = string.IsNullOrWhiteSpace(request.IdentityCardUrl) ? null : request.IdentityCardUrl.Trim();
        profile.UpdatedAt = now;
        profile.Sports.Clear();

        var specialtyIds = request.SpecialtySportIds?.Distinct().ToList() ?? new List<Guid>();
        if (specialtyIds.Count > 0)
        {
            var specialties = await _sports.ListByIdsAsync(specialtyIds);
            foreach (var specialty in specialties)
            {
                profile.Sports.Add(specialty);
            }
        }

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
            GatewayId = "PAYOS",
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
            return ApiResponseDto<ActivationLinkDto>.Fail("Could not save your information. The email or phone number may already be in use.");
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
