using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payments;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPaymentGateway _paymentGateway;

    public PaymentService(
        IApplicationDbContext context,
        IOtpService otpService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IPaymentGateway paymentGateway)
    {
        _context = context;
        _otpService = otpService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _paymentGateway = paymentGateway;
    }

    /// <summary>
    /// Validates the coach draft BEFORE paying (model + email availability).
    /// </summary>
    public async Task<ApiResponseDto<CoachActivationPreviewDto>> PrepareCoachActivationAsync(RegisterCoachRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            return ApiResponseDto<CoachActivationPreviewDto>.Fail("This email is already in use.");
        }

        return ApiResponseDto<CoachActivationPreviewDto>.Ok(
            new CoachActivationPreviewDto
            {
                Email = normalizedEmail,
                AmountVnd = PaymentConstants.CoachActivationFeeVnd,
                Currency = PaymentConstants.CurrencyVnd,
                OrderType = PaymentConstants.OrderTypeCoachActivation
            },
            "Order preview created. Proceed to payment.");
    }

    /// <summary>
    /// Verifies the payment, then creates the coach account + activation order + payment record
    /// in a single transaction. The account only exists after successful payment.
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> ConfirmCoachActivationAsync(ConfirmCoachActivationRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.Password != request.ConfirmPassword)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Passwords do not match.");
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This email is already in use.");
        }

        // Check for duplicate phone number (Users.PhoneNumber is UNIQUE)
        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (normalizedPhone != null &&
            await _context.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This phone number is already in use.");
        }

        // 1. Verify the payment with the gateway (mock: refs starting with "FAIL" are declined).
        //    Replace with a real VNPay/MoMo verification call when merchant credentials exist.
        if (string.IsNullOrWhiteSpace(request.TransactionRef) ||
            request.TransactionRef.Trim().StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment verification failed. No account was created.");
        }

        // 2. Verify the OTP (once)
        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpConstants.PurposeRegisterCoach);

        if (!otpValid)
        {
            return ApiResponseDto<AuthResponseDto>.Fail(otpMessage);
        }

        var now = DateTime.UtcNow;
        var fee = PaymentConstants.CoachActivationFeeVnd;

        // 3. Create the coach user + pending approval profile
        var newUser = new User
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
            IsLocked = false,
            CreatedAt = now,
            UpdatedAt = now,
            CoachProfileCoach = new CoachProfile
            {
                ApprovalStatus = "PENDING",
                ExperienceYears = request.ExperienceYears,
                Bio = string.IsNullOrWhiteSpace(request.Biography) ? null : request.Biography.Trim(),
                CertificateUrl = string.IsNullOrWhiteSpace(request.CertificateUrl) ? null : request.CertificateUrl.Trim(),
                IdentityCardUrl = string.IsNullOrWhiteSpace(request.IdentityCardUrl) ? null : request.IdentityCardUrl.Trim(),
                UpdatedAt = now
            }
        };
        // CoachId shares the UserId (set explicitly for clarity)
        newUser.CoachProfileCoach.CoachId = newUser.UserId;

        // Coaching specialties (multiple allowed) -> CoachSports
        var specialtyIds = request.SpecialtySportIds?.Distinct().ToList() ?? new List<Guid>();
        if (specialtyIds.Count > 0)
        {
            var specialties = await _context.Sports
                .Where(s => specialtyIds.Contains(s.SportId))
                .ToListAsync();
            foreach (var specialty in specialties)
            {
                newUser.CoachProfileCoach.Sports.Add(specialty);
            }
        }

        _context.Users.Add(newUser);

        // 4. Activation order + detail + payment record (Orders.TraineeID is NOT NULL -> created with the user)
        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            TraineeId = newUser.UserId,
            CoachId = newUser.UserId,
            TotalAmount = fee,
            OrderStatus = PaymentConstants.OrderStatusPaid,
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
            Method = string.IsNullOrWhiteSpace(request.Method) ? PaymentConstants.GatewayMock : request.Method.Trim(),
            GatewayId = PaymentConstants.GatewayMock,
            TransactionRef = request.TransactionRef.Trim(),
            Status = PaymentConstants.PaymentStatusSuccess,
            ProcessedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        _context.Orders.Add(order);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not create the account. The email or phone number may already be in use.");
        }

        // 5. Sign the coach in immediately
        var responseData = BuildAuthResponse(newUser);

        return ApiResponseDto<AuthResponseDto>.Ok(responseData, "Payment successful! Coach account created.");
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

        var existing = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (existing != null && existing.IsLocked != true)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("This email is already in use.");
        }

        // Check for duplicate phone number (Users.PhoneNumber is UNIQUE, excluding self on retry)
        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (normalizedPhone != null &&
            await _context.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone &&
                (existing == null || u.UserId != existing.UserId)))
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("This phone number is already in use.");
        }

        // Verify the OTP once (user may resend a fresh code for retries)
        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpConstants.PurposeRegisterCoach);

        if (!otpValid)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail(otpMessage);
        }

        var now = DateTime.UtcNow;
        var fee = PaymentConstants.CoachActivationFeeVnd;

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
                IsLocked = true, // Locked until the activation fee is paid
                CreatedAt = now,
                UpdatedAt = now,
                CoachProfileCoach = new CoachProfile
                {
                    ApprovalStatus = "PENDING",
                    UpdatedAt = now
                }
            };
            user.CoachProfileCoach.CoachId = user.UserId;
            _context.Users.Add(user);
            profile = user.CoachProfileCoach;
        }
        else
        {
            // Retry: refresh the locked account with the latest form data
            user = existing;
            user.FullName = request.FullName.Trim();
            user.PasswordHash = _passwordHasher.HashPassword(request.Password);
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
            user.UpdatedAt = now;

            // Cancel its previous pending activation orders
            var staleOrders = await _context.Orders
                .Where(o => o.TraineeId == user.UserId
                    && o.OrderType == PaymentConstants.OrderTypeCoachActivation
                    && o.OrderStatus == PaymentConstants.OrderStatusPending)
                .ToListAsync();
            foreach (var stale in staleOrders)
            {
                stale.OrderStatus = PaymentConstants.OrderStatusCancelled;
            }

            profile = await _context.CoachProfiles
                .Include(p => p.Sports)
                .FirstOrDefaultAsync(p => p.CoachId == user.UserId);
            if (profile == null)
            {
                profile = new CoachProfile { CoachId = user.UserId };
                _context.CoachProfiles.Add(profile);
            }
        }

        // Refresh profile fields + specialties on the coach profile
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
            var specialties = await _context.Sports
                .Where(s => specialtyIds.Contains(s.SportId))
                .ToListAsync();
            foreach (var specialty in specialties)
            {
                profile.Sports.Add(specialty);
            }
        }

        // Unique PayOS order code: yyMMddHHmmss + 3 random digits
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
        _context.Orders.Add(order);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<ActivationLinkDto>.Fail("Could not save your information. The email or phone number may already be in use.");
        }

        // Create the PayOS VietQR payment link
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
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.GatewayTransactionId == orderCode.ToString());

        if (payment == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment order not found.");
        }

        var order = await _context.Orders.FindAsync(payment.OrderId);
        if (order == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Payment order not found.");
        }

        if (order.OrderStatus == PaymentConstants.OrderStatusPaid)
        {
            var existingUser = await _context.Users.FindAsync(order.TraineeId);
            if (existingUser == null || existingUser.IsLocked == true)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Account is not available.");
            }
            return ApiResponseDto<AuthResponseDto>.Ok(BuildAuthResponse(existingUser), "Payment already confirmed. Welcome back!");
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

        var user = await _context.Users.FindAsync(order.TraineeId);
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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not activate the account. Please try again.");
        }

        return ApiResponseDto<AuthResponseDto>.Ok(BuildAuthResponse(user), "Payment successful! Coach account created.");
    }

    public async Task<ApiResponseDto<bool>> CancelActivationAsync(long orderCode)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.GatewayTransactionId == orderCode.ToString());

        if (payment == null)
        {
            return ApiResponseDto<bool>.Fail("Payment order not found.");
        }

        var order = await _context.Orders.FindAsync(payment.OrderId);
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

        await _context.SaveChangesAsync();

        return ApiResponseDto<bool>.Ok(true, "Payment order cancelled.");
    }

    private AuthResponseDto BuildAuthResponse(User user)
    {
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponseDto
        {
            AccessToken = token,
            RefreshToken = Guid.NewGuid().ToString("N"),
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.UserId,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                RoleCode = user.RoleCode,
                AvatarUrl = user.AvatarUrl
            }
        };
    }
}
