using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace FitSocial.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenBlacklistService _tokenBlacklist;
    private readonly ITermsAndPolicyRepository _terms;
    private readonly IUserAgreementRepository _agreements;
    private readonly ICoachEkycVerificationRepository _ekycs;
    private readonly ICoachCertificateRepository _certificates;
    private readonly ICoachProfileRepository _coachProfiles;
    private readonly IEkycService _ekycService;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ITokenBlacklistService tokenBlacklist,
        ITermsAndPolicyRepository terms,
        IUserAgreementRepository agreements,
        ICoachEkycVerificationRepository ekycs,
        ICoachCertificateRepository certificates,
        ICoachProfileRepository coachProfiles,
        IEkycService ekycService)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _tokenBlacklist = tokenBlacklist;
        _terms = terms;
        _agreements = agreements;
        _ekycs = ekycs;
        _certificates = certificates;
        _coachProfiles = coachProfiles;
        _ekycService = ekycService;
    }

    public async Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var emailExists = await _users.ExistsByEmailAsync(normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<bool>.Fail("This email is already registered. Please use another email or log in.");
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) 
            ? OtpConstants.PurposeRegisterTrainee 
            : request.Purpose;

        var otpCode = await _otpService.GenerateOtpAsync(normalizedEmail, purpose);

        var subject = "[FitSocial] Account registration verification code";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <img src=""https://res.cloudinary.com/avvuvfw6/image/upload/v1789397609/fitsocial/credentials/Logo_FitSocial_agntvx.jpg"" alt=""FitSocial"" style=""width: 72px; height: 72px; border-radius: 16px;"" />
                    <h2 style=""color: #FF5722; margin: 8px 0 0;"">FitSocial</h2>
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

    public async Task<ApiResponseDto<bool>> VerifyOtpAsync(VerifyOtpRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();
        var purpose = string.IsNullOrWhiteSpace(request.Purpose) ? OtpConstants.PurposeRegisterTrainee : request.Purpose;
        var (ok, msg) = await _otpService.CheckOtpAsync(normalizedEmail, request.OtpCode.Trim(), purpose);
        if (!ok) return ApiResponseDto<bool>.Fail(msg);
        return ApiResponseDto<bool>.Ok(true, "OTP verified.");
    }

    /// <summary>
    /// Server-side sign-out (UC_03): puts the access token ID on the Redis
    /// blacklist until its natural expiry, and revokes the refresh token.
    /// The JWT middleware rejects the access token afterwards.
    /// </summary>
    public async Task<ApiResponseDto<bool>> LogoutAsync(string? jti, DateTime? expiresAtUtc, string? refreshToken = null)
    {
        if (!string.IsNullOrWhiteSpace(jti) && expiresAtUtc.HasValue)
        {
            var remaining = expiresAtUtc.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await _tokenBlacklist.RevokeTokenAsync(jti, remaining);
            }
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        return ApiResponseDto<bool>.Ok(true, "Signed out successfully.");
    }

    /// <summary>
    /// Email + password sign-in shared by all roles (Trainee, Coach, ...).
    /// The role travels in the JWT (ClaimTypes.Role) and AuthResponse.User.
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> LoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var user = await _users.FindByEmailAsync(normalizedEmail);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
        }

        if (user.IsLocked == true)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This account has been locked. Please contact an administrator.");
        }

        if (!(string.Equals(user.RoleCode?.Trim(), RoleConstants.Trainee, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.RoleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
        }

        if (!string.IsNullOrWhiteSpace(request.RoleCode) &&
            !string.Equals(user.RoleCode?.Trim(), request.RoleCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
        }

        user.LastActiveAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        var loginResponse = await _tokenService.CreateSessionAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponseDto<AuthResponseDto>.Ok(loginResponse, "Signed in successfully!");
    }

    /// <summary>
    /// Back-office sign-in for Staff/Admin only (UC_28 Management Portal).
    /// Same credentials flow as client login, but Trainee/Coach are rejected here.
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> AdminLoginAsync(LoginRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        var user = await _users.FindByEmailAsync(normalizedEmail);

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
        }

        if (user.IsLocked == true)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This account has been locked. Please contact an administrator.");
        }

        if (!(string.Equals(user.RoleCode?.Trim(), RoleConstants.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.RoleCode?.Trim(), RoleConstants.Staff, StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
        }

        user.LastActiveAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        var adminResponse = await _tokenService.CreateSessionAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponseDto<AuthResponseDto>.Ok(adminResponse, "Welcome to the Management Portal!");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.Password != request.ConfirmPassword)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Passwords do not match.");
        }

        var emailExists = await _users.ExistsByEmailAsync(normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This email is already in use.");
        }

        if (request.TermId == null || request.TermId == Guid.Empty)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("You must agree to the Terms and Privacy Policy.");
        }

        var term = await _terms.GetByIdAsync(request.TermId.Value);
        if (term == null)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Selected terms version is not valid.");
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (normalizedPhone != null &&
            await _users.ExistsByPhoneAsync(normalizedPhone))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This phone number is already in use.");
        }

        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpConstants.PurposeRegisterTrainee);

        if (!otpValid)
        {
            return ApiResponseDto<AuthResponseDto>.Fail(otpMessage);
        }

        var now = DateTime.UtcNow;
        var newUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
            DateOfBirth = request.DateOfBirth,
            RoleCode = RoleConstants.Trainee,
            IsInternal = false,
            IsLocked = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _users.AddAsync(newUser);

        // Record agreement (same as Coach flow, without IP)
        await _agreements.AddAsync(new UserAgreement
        {
            AgreementId = Guid.NewGuid(),
            UserId = newUser.UserId,
            TermId = term.TermId,
            AcceptedAt = now
        });

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not create the account. The email or phone number may already be in use.");
        }

        var responseData = await _tokenService.CreateSessionAsync(newUser);

        return ApiResponseDto<AuthResponseDto>.Ok(responseData, "Trainee account registered successfully!");
    }

    /// <summary>
    /// Register a Coach account (pending, locked until payment). Creates User (locked), CoachProfile PENDING,
    /// eKYC verification, certificates, and terms agreement. Used by the 5-step coach flow before payment.
    /// </summary>
    public async Task<ApiResponseDto<User>> RegisterCoachAsync(RegisterCoachRequestDto request, string? ipAddress = null)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.Password != request.ConfirmPassword)
            return ApiResponseDto<User>.Fail("Passwords do not match.");
        if (string.IsNullOrWhiteSpace(request.FrontCardUrl) || string.IsNullOrWhiteSpace(request.BackCardUrl))
            return ApiResponseDto<User>.Fail("Front and back ID card images are required.");
        if (string.IsNullOrWhiteSpace(request.FaceImageUrl))
            return ApiResponseDto<User>.Fail("Live face image is required.");
        if (request.TermId == null || request.TermId == Guid.Empty)
            return ApiResponseDto<User>.Fail("You must agree to the Terms and Privacy Policy.");

        var term = await _terms.GetByIdAsync(request.TermId.Value);
        if (term == null)
            return ApiResponseDto<User>.Fail("Selected terms version is not valid.");

        var existing = await _users.FindByEmailAsync(normalizedEmail);
        if (existing != null && existing.IsLocked != true)
            return ApiResponseDto<User>.Fail("This email is already in use.");

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (normalizedPhone != null && await _users.ExistsByPhoneAsync(normalizedPhone, existing?.UserId))
            return ApiResponseDto<User>.Fail("This phone number is already in use.");

        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(normalizedEmail, request.OtpCode.Trim(), OtpConstants.PurposeRegisterCoach);
        if (!otpValid)
            return ApiResponseDto<User>.Fail(otpMessage);

        var ekycResult = await _ekycService.VerifyAsync(new FitSocial.Application.DTOs.Coach.EkycVerificationDto
        {
            FrontCardUrl = request.FrontCardUrl!,
            BackCardUrl = request.BackCardUrl!,
            FaceImageUrl = request.FaceImageUrl,
            FaceImageLeftUrl = request.FaceImageLeftUrl,
            FaceImageRightUrl = request.FaceImageRightUrl,
            FaceImageTopUrl = request.FaceImageTopUrl,
            FaceImageBottomUrl = request.FaceImageBottomUrl
        });
        if (!ekycResult.Success)
            return ApiResponseDto<User>.Fail(ekycResult.Error ?? "ID verification failed.");

        // 1 person = 1 CCCD: block if this IdCardNumber already belongs to another coach
        if (await _ekycs.ExistsByIdCardNumberAsync(ekycResult.Result.IdCardNumber, existing?.UserId))
            return ApiResponseDto<User>.Fail("This ID card has already been used for another coach.");

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
                PhoneNumber = normalizedPhone,
                Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim(),
                DateOfBirth = request.DateOfBirth,
                RoleCode = RoleConstants.Coach,
                IsInternal = false,
                IsLocked = true,
                CreatedAt = now,
                UpdatedAt = now,
                CoachProfileCoach = new CoachProfile { ApprovalStatus = "PENDING", UpdatedAt = now }
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
            user.PhoneNumber = normalizedPhone;
            user.Gender = string.IsNullOrWhiteSpace(request.Gender) ? null : request.Gender.Trim();
            user.DateOfBirth = request.DateOfBirth;
            user.UpdatedAt = now;
            profile = await _coachProfiles.GetByIdAsync(user.UserId);
            if (profile == null)
            {
                profile = new CoachProfile { CoachId = user.UserId };
                await _coachProfiles.AddAsync(profile);
            }
        }

        profile.ApprovalStatus = "PENDING";
        profile.ExperienceYears = request.ExperienceYears;
        profile.Bio = string.IsNullOrWhiteSpace(request.Biography) ? null : request.Biography.Trim();
        var firstCertUrl = request.Certificates?.FirstOrDefault()?.CertificateUrl ?? request.CertificateUrl;
        profile.CertificateUrl = string.IsNullOrWhiteSpace(firstCertUrl) ? null : firstCertUrl.Trim();
        profile.IdentityCardUrl = request.FrontCardUrl!.Trim();
        profile.UpdatedAt = now;

        // Replace old eKYC/certificates on retry (each person has only 1 CCCD)
        if (existing != null)
        {
            var oldEkycs = await _ekycs.ListByCoachIdAsync(user.UserId);
            if (oldEkycs.Any()) _ekycs.RemoveRange(oldEkycs);
            var oldCerts = await _certificates.ListByCoachIdAsync(user.UserId);
            if (oldCerts.Any()) _certificates.RemoveRange(oldCerts);
        }

        await _ekycs.AddAsync(new CoachEkycVerification
        {
            EkycId = Guid.NewGuid(),
            CoachId = user.UserId,
            IdCardNumber = ekycResult.Result.IdCardNumber,
            FullNameOnCard = ekycResult.Result.FullNameOnCard,
            DateOfBirthOnCard = ekycResult.Result.DateOfBirthOnCard,
            Birthplace = ekycResult.Result.Birthplace,
            Sex = ekycResult.Result.Sex,
            Address = ekycResult.Result.Address,
            Province = ekycResult.Result.Province,
            District = ekycResult.Result.District,
            Ward = ekycResult.Result.Ward,
            ProvinceCode = ekycResult.Result.ProvinceCode,
            DistrictCode = ekycResult.Result.DistrictCode,
            WardCode = ekycResult.Result.WardCode,
            Street = ekycResult.Result.Street,
            Nationality = ekycResult.Result.Nationality,
            Religion = ekycResult.Result.Religion,
            Ethnicity = ekycResult.Result.Ethnicity,
            Expiry = ekycResult.Result.Expiry,
            Feature = ekycResult.Result.Feature,
            IssueDate = ekycResult.Result.IssueDate,
            IssueBy = ekycResult.Result.IssueBy,
            DocumentType = ekycResult.Result.DocumentType,
            LivenessScore = ekycResult.Result.LivenessScore,
            FaceMatchConfidence = ekycResult.Result.FaceMatchConfidence,
            RawInformationJson = ekycResult.Result.RawInformationJson,
            FrontCardUrl = request.FrontCardUrl!.Trim(),
            BackCardUrl = request.BackCardUrl!.Trim(),
            FaceImageUrl = request.FaceImageUrl?.Trim(),
            VerificationStatus = ekycResult.Result.VerificationStatus,
            FailureReason = ekycResult.Result.FailureReason,
            CreatedAt = now,
            UpdatedAt = now
        });

        if (request.Certificates != null)
        {
            foreach (var cert in request.Certificates.Where(c => !string.IsNullOrWhiteSpace(c.CertificateUrl)))
            {
                await _certificates.AddAsync(new CoachCertificate
                {
                    CertificateId = Guid.NewGuid(),
                    CoachId = user.UserId,
                    CertificateName = string.IsNullOrWhiteSpace(cert.CertificateName) ? null : cert.CertificateName.Trim(),
                    CertificateUrl = cert.CertificateUrl.Trim(),
                    IssuedDate = cert.IssuedDate,
                    CreatedAt = now
                });
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.CertificateUrl))
        {
            await _certificates.AddAsync(new CoachCertificate
            {
                CertificateId = Guid.NewGuid(),
                CoachId = user.UserId,
                CertificateName = "Primary Certificate",
                CertificateUrl = request.CertificateUrl.Trim(),
                CreatedAt = now
            });
        }

        var existingAgreement = await _agreements.FindByUserAndTermAsync(user.UserId, term.TermId);
        if (existingAgreement != null)
        {
            existingAgreement.IpAddress = ipAddress;
            existingAgreement.AcceptedAt = now;
        }
        else
        {
            await _agreements.AddAsync(new UserAgreement
            {
                AgreementId = Guid.NewGuid(),
                UserId = user.UserId,
                TermId = term.TermId,
                IpAddress = ipAddress,
                AcceptedAt = now
            });
        }

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IdCardNumber") == true)
        {
            return ApiResponseDto<User>.Fail("This ID card has already been used for another coach.");
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<User>.Fail("Could not save your information. The email or phone number may already be in use.");
        }

        return ApiResponseDto<User>.Ok(user);
    }

    /// <summary>
    /// OAuth2 authorization-code flow (popup, no FedCM): exchanges the code for tokens
    /// server-side, then signs the user in with the same link-or-create rules as ID-token login.
    /// Works in Guest/Incognito windows where the FedCM button flow is blocked.
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> GoogleCodeLoginAsync(GoogleCodeRequestDto request)
    {
        var clientId = _configuration["Google:ClientId"];
        var clientSecret = _configuration["Google:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Google sign-in is not configured on the server.");
        }

        string? idToken;
        var redirectUri = string.IsNullOrWhiteSpace(request.RedirectUri) ? "postmessage" : request.RedirectUri.Trim();

        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = request.Code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }); 

            using var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Google authorization failed. Please try again.");
            }

            using var tokenJson = await System.Text.Json.JsonDocument.ParseAsync(
                await tokenResponse.Content.ReadAsStreamAsync());

            if (!tokenJson.RootElement.TryGetProperty("id_token", out var idTokenEl))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Google authorization failed. Please try again.");
            }

            idToken = idTokenEl.GetString();
        }
        catch
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not reach Google. Please check your connection and try again.");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Google authorization failed. Please try again.");
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
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

        var user = await _users.FindByGoogleSubAsync(googleSub) ?? await _users.FindByEmailAsync(normalizedEmail);

        if (user == null)
        {
            if (string.Equals(request.RoleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Google sign-up is available for Trainee accounts only. To become a coach, please use the Coach application form.");
            }

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

            await _users.AddAsync(user);
        }
        else
        {
            if (user.IsLocked == true)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("This account has been locked. Please contact an administrator.");
            }

            if (!(string.Equals(user.RoleCode?.Trim(), RoleConstants.Trainee, StringComparison.OrdinalIgnoreCase)
                || string.Equals(user.RoleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase)))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
            }

            if (!string.IsNullOrWhiteSpace(request.RoleCode) &&
                !string.Equals(user.RoleCode?.Trim(), request.RoleCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Invalid email or password.");
            }

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

        try
        {
            var googleResponse = await _tokenService.CreateSessionAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return ApiResponseDto<AuthResponseDto>.Ok(googleResponse, "Google sign-in successful!");
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not sign you in. Please try again.");
        }
    }

    /// <summary>
    /// Exchanges a valid refresh token for a brand-new session (rotation).
    /// </summary>
    public async Task<ApiResponseDto<AuthResponseDto>> RefreshAsync(RefreshRequestDto request)
    {
        return await _tokenService.RefreshSessionAsync(request.RefreshToken);
    }

    /// <summary>
    /// Step 1 of UC_04: send a password-reset OTP to the email.
    /// Always returns success to avoid revealing which emails are registered.
    /// </summary>
    public async Task<ApiResponseDto<bool>> ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();
        const string genericOk = "If this email is registered, a reset code has been sent to it.";

        var user = await _users.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return ApiResponseDto<bool>.Ok(true, genericOk);
        }

        var otpCode = await _otpService.GenerateOtpAsync(normalizedEmail, OtpConstants.PurposeResetPassword);

        var subject = "[FitSocial] Password reset verification code";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <img src=""https://res.cloudinary.com/avvuvfw6/image/upload/v1789397609/fitsocial/credentials/Logo_FitSocial_agntvx.jpg"" alt=""FitSocial"" style=""width: 72px; height: 72px; border-radius: 16px;"" />
                    <h2 style=""color: #FF5722; margin: 8px 0 0;"">FitSocial</h2>
                    <p style=""color: #666; margin: 5px 0 0;"">Sports & Fitness Community</p>
                </div>
                <div style=""background: #fff3e0; border-left: 4px solid #FF5722; padding: 15px; margin-bottom: 20px;"">
                    <p style=""margin: 0; color: #333; font-size: 16px;"">Hello,</p>
                    <p style=""margin: 10px 0 0; color: #555;"">You requested a password reset for your FitSocial account. Your OTP verification code is:</p>
                </div>
                <div style=""text-align: center; margin: 30px 0;"">
                    <span style=""font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #FF5722; background: #fbe9e7; padding: 10px 24px; border-radius: 8px; border: 1px dashed #FF5722;"">
                        {otpCode}
                    </span>
                </div>
                <p style=""color: #777; font-size: 14px; text-align: center;"">This code is valid for <strong>5 minutes</strong>. Never share it with anyone.</p>
                <hr style=""border: none; border-top: 1px solid #eee; margin: 25px 0;"" />
                <p style=""color: #999; font-size: 12px; text-align: center; margin: 0;"">If you did not request this, please ignore this email and consider securing your account.</p>
            </div>";

        await _emailService.SendEmailAsync(normalizedEmail, subject, body);

        return ApiResponseDto<bool>.Ok(true, genericOk);
    }

    /// <summary>
    /// Step 2 of UC_04: verify the OTP and set the new password.
    /// Bumps TokenVersion so every existing session/token dies immediately.
    /// Also works for Google-created accounts (gives them a usable password).
    /// </summary>
    public async Task<ApiResponseDto<bool>> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponseDto<bool>.Fail("Passwords do not match.");
        }

        var user = await _users.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            return ApiResponseDto<bool>.Fail("Invalid email or OTP code.");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash) &&
            _passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return ApiResponseDto<bool>.Fail("New password must be different from the current password.");
        }

        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            request.OtpCode.Trim(),
            OtpConstants.PurposeResetPassword);

        if (!otpValid)
        {
            return ApiResponseDto<bool>.Fail(otpMessage);
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.TokenVersion += 1; 
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<bool>.Fail("Could not reset the password. Please try again.");
        }

        return ApiResponseDto<bool>.Ok(true, "Password reset successfully. Please sign in with your new password.");
    }

    /// <summary>
    /// UC_05.2: change password while signed in. Requires the current password
    /// (except passwordless Google accounts, which may set one directly).
    /// Bumps TokenVersion so every other session/token dies immediately;
    /// the client signs out locally and asks for a fresh sign-in.
    /// </summary>
    public async Task<ApiResponseDto<bool>> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ApiResponseDto<bool>.Fail("Passwords do not match.");
        }

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
        {
            return ApiResponseDto<bool>.Fail("Account is not available.");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash) &&
            !_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return ApiResponseDto<bool>.Fail("Current password is incorrect.");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash) &&
            _passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return ApiResponseDto<bool>.Fail("New password must be different from the current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.TokenVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<bool>.Fail("Could not change the password. Please try again.");
        }

        return ApiResponseDto<bool>.Ok(true, "Password changed successfully. Please sign in again.");
    }

    public async Task<ApiResponseDto<UserDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null || user.IsLocked == true)
        {
            return ApiResponseDto<UserDto>.Fail("User not found or account is locked.");
        }

        var dto = new UserDto
        {
            Id = user.UserId,
            FullName = user.FullName ?? string.Empty,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            RoleCode = user.RoleCode,
            AvatarUrl = user.AvatarUrl
        };

        return ApiResponseDto<UserDto>.Ok(dto);
    }
}
