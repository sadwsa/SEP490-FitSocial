using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FitSocial.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IConfiguration _configuration;

    public AuthService(
        IApplicationDbContext context,
        IOtpService otpService,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IConfiguration configuration)
    {
        _context = context;
        _otpService = otpService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _configuration = configuration;
    }

    public async Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        // Check whether the email already exists in the system
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<bool>.Fail("This email is already registered. Please use another email or log in.");
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) 
            ? OtpConstants.PurposeRegisterTrainee 
            : request.Purpose;

        // Generate the OTP and save its hash to OTPLogs
        var otpCode = await _otpService.GenerateOtpAsync(normalizedEmail, purpose);

        // Send the email containing the OTP code
        var subject = "[FitSocial] Account registration verification code";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <h2 style=""color: #FF5722; margin: 0;"">FitSocial 🔥</h2>
                    <p style=""color: #666; margin: 5px 0 0;"">Sports & Fitness Community</p>
                </div>
                <div style=""background: #fff3e0; border-left: 4px solid #FF5722; padding: 15px; margin-bottom: 20px;"">
                    <p style=""margin: 0; color: #333; font-size: 16px;"">Hello,</p>
                    <p style=""margin: 10px 0 0; color: #555;"">You are registering a Trainee account at FitSocial. Your OTP verification code is:</p>
                </div>
                <div style=""text-align: center; margin: 30px 0;"">
                    <span style=""font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #FF5722; background: #fbe9e7; padding: 10px 24px; border-radius: 8px; border: 1px dashed #FF5722;"">
                        {otpCode}
                    </span>
                </div>
                <p style=""color: #777; font-size: 14px; text-align: center;"">This code is valid for <strong>5 minutes</strong>. Never share it with anyone.</p>
                <hr style=""border: none; border-top: 1px solid #eee; margin: 25px 0;"" />
                <p style=""color: #999; font-size: 12px; text-align: center; margin: 0;"">If you did not request this code, please ignore this email.</p>
            </div>";

        await _emailService.SendEmailAsync(normalizedEmail, subject, body);

        return ApiResponseDto<bool>.Ok(true, "The OTP verification code has been sent to your email.");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request)
    {
        return await RegisterAsync(
            request.FullName,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            request.OtpCode,
            request.PhoneNumber,
            request.Gender,
            request.DateOfBirth,
            request.FavoriteSportIds,
            OtpConstants.PurposeRegisterTrainee,
            RoleConstants.Trainee);
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RegisterCoachAsync(RegisterCoachRequestDto request)
    {
        return await RegisterAsync(
            request.FullName,
            request.Email,
            request.Password,
            request.ConfirmPassword,
            request.OtpCode,
            request.PhoneNumber,
            request.Gender,
            request.DateOfBirth,
            request.FavoriteSportIds,
            OtpConstants.PurposeRegisterCoach,
            RoleConstants.Coach,
            request.ExperienceYears,
            request.Biography,
            request.CertificateUrl,
            request.SpecialtySportIds,
            request.IdentityCardUrl);
    }

    private async Task<ApiResponseDto<AuthResponseDto>> RegisterAsync(
        string fullName,
        string email,
        string password,
        string confirmPassword,
        string otpCode,
        string? phoneNumber,
        string? gender,
        DateOnly? dateOfBirth,
        List<Guid>? favoriteSportIds,
        string otpPurpose,
        string roleCode,
        int? experienceYears = null,
        string? biography = null,
        string? certificateUrl = null,
        List<Guid>? specialtySportIds = null,
        string? identityCardUrl = null)
    {
        var normalizedEmail = email.Trim().ToLower();

        if (password != confirmPassword)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Passwords do not match.");
        }

        // Check for duplicate email
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This email is already in use.");
        }

        // Check for duplicate phone number (Users.PhoneNumber is UNIQUE)
        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (normalizedPhone != null &&
            await _context.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This phone number is already in use.");
        }

        // Verify the OTP
        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            otpCode.Trim(),
            otpPurpose);

        if (!otpValid)
        {
            return ApiResponseDto<AuthResponseDto>.Fail(otpMessage);
        }

        // Create the new user
        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(password),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
            DateOfBirth = dateOfBirth,
            RoleCode = roleCode,
            IsInternal = false,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Coach accounts start with a pending approval profile
        if (roleCode == RoleConstants.Coach)
        {
            newUser.CoachProfileCoach = new CoachProfile
            {
                CoachId = newUser.UserId,
                ExperienceYears = experienceYears,
                Bio = string.IsNullOrWhiteSpace(biography) ? null : biography.Trim(),
                CertificateUrl = string.IsNullOrWhiteSpace(certificateUrl) ? null : certificateUrl.Trim(),
                IdentityCardUrl = string.IsNullOrWhiteSpace(identityCardUrl) ? null : identityCardUrl.Trim(),
                ApprovalStatus = "PENDING",
                UpdatedAt = DateTime.UtcNow
            };

            // Coaching specialties (multiple allowed) -> saved to CoachSports
            var specialtyIds = specialtySportIds?.Distinct().ToList() ?? new List<Guid>();
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
        }

        // Attach favorite sports (if any) -> saved to UserFavoriteSports
        var sportIds = favoriteSportIds?.Distinct().ToList() ?? new List<Guid>();
        if (sportIds.Count > 0)
        {
            var sports = await _context.Sports
                .Where(s => sportIds.Contains(s.SportId))
                .ToListAsync();
            foreach (var sport in sports)
            {
                newUser.Sports.Add(sport);
            }
        }

        _context.Users.Add(newUser);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // E.g. race on UNIQUE email/phone between the check above and the insert
            return ApiResponseDto<AuthResponseDto>.Fail("Could not create the account. The email or phone number may already be in use.");
        }

        var responseData = BuildAuthResponse(newUser);

        var roleLabel = roleCode == RoleConstants.Coach ? "Coach" : "Trainee";
        return ApiResponseDto<AuthResponseDto>.Ok(responseData, $"{roleLabel} account registered successfully!");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> GoogleLoginAsync(GoogleLoginRequestDto request)
    {
        var clientId = _configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Google sign-in is not configured on the server.");
        }

        // Validate the ID Token with Google (signature + audience + expiry)
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
        }
        catch (InvalidJwtException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Google ID Token is invalid or has expired.");
        }

        if (!payload.EmailVerified)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Google email is not verified.");
        }

        var googleSub = payload.Subject;
        var normalizedEmail = payload.Email.Trim().ToLower();

        // Find the Google-linked user, otherwise find by email to link
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.GoogleProviderId == googleSub);

        if (user == null)
        {
            user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        }

        if (user == null)
        {
            // Google sign-up is Trainee-only. Coach accounts must use
            // the application form (register-coach + activation payment).
            if (string.Equals(request.RoleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Google sign-up is available for Trainee accounts only. To become a coach, please use the Coach application form.");
            }

            // New email -> auto-create a Google-linked Trainee account
            user = new User
            {
                UserId = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = string.IsNullOrWhiteSpace(payload.Name) ? normalizedEmail : payload.Name.Trim(),
                PasswordHash = null,
                GoogleProviderId = googleSub,
                AvatarUrl = string.IsNullOrWhiteSpace(payload.Picture) ? null : payload.Picture,
                RoleCode = RoleConstants.Trainee,
                IsInternal = false,
                IsLocked = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
        }
        else
        {
            if (user.IsLocked == true)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("This account has been locked. Please contact an administrator.");
            }

            // Link the Google account to the existing email
            if (string.IsNullOrWhiteSpace(user.GoogleProviderId))
            {
                user.GoogleProviderId = googleSub;
            }
            if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(payload.Picture))
            {
                user.AvatarUrl = payload.Picture;
            }
            user.LastActiveAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var responseData = BuildAuthResponse(user);
        return ApiResponseDto<AuthResponseDto>.Ok(responseData, "Google sign-in successful!");
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
