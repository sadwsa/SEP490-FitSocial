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
    private readonly ISportRepository _sports;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthService(
        IUserRepository users,
        ISportRepository sports,
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _users = users;
        _sports = sports;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

        if (!IsClientLoginAllowed(user.RoleCode))
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
        await _unitOfWork.SaveChangesAsync();

        return ApiResponseDto<AuthResponseDto>.Ok(BuildAuthResponse(user), "Signed in successfully!");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request)    {
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

        var emailExists = await _users.ExistsByEmailAsync(normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This email is already in use.");
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        if (normalizedPhone != null &&
            await _users.ExistsByPhoneAsync(normalizedPhone))
        {
            return ApiResponseDto<AuthResponseDto>.Fail("This phone number is already in use.");
        }

        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail,
            otpCode.Trim(),
            otpPurpose);

        if (!otpValid)
        {
            return ApiResponseDto<AuthResponseDto>.Fail(otpMessage);
        }

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

            var specialtyIds = specialtySportIds?.Distinct().ToList() ?? new List<Guid>();
            if (specialtyIds.Count > 0)
            {
                var specialties = await _sports.ListByIdsAsync(specialtyIds);
                foreach (var specialty in specialties)
                {
                    newUser.CoachProfileCoach.Sports.Add(specialty);
                }
            }
        }

        var sportIds = favoriteSportIds?.Distinct().ToList() ?? new List<Guid>();
        if (sportIds.Count > 0)
        {
            var sports = await _sports.ListByIdsAsync(sportIds);
            foreach (var sport in sports)
            {
                newUser.Sports.Add(sport);
            }
        }

        await _users.AddAsync(newUser);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not create the account. The email or phone number may already be in use.");
        }

        var responseData = BuildAuthResponse(newUser);

        var roleLabel = roleCode == RoleConstants.Coach ? "Coach" : "Trainee";
        return ApiResponseDto<AuthResponseDto>.Ok(responseData, $"{roleLabel} account registered successfully!");
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
        var redirectUri = string.IsNullOrWhiteSpace(request.RedirectUri)
            ? "postmessage"
            : request.RedirectUri.Trim();
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
            using var tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token", tokenRequest);
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
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
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

        var user = await _users.FindByGoogleSubAsync(googleSub);

        if (user == null)
        {
            user = await _users.FindByEmailAsync(normalizedEmail);
        }

        if (user == null)
        {
            if (string.Equals(request.RoleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponseDto<AuthResponseDto>.Fail("Google sign-up is available for Trainee accounts only. To become a coach, please use the Coach application form.");
            }

            user = BuildGoogleUser(payload, normalizedEmail, googleSub);
            await _users.AddAsync(user);
        }
        else
        {
            if (user.IsLocked == true)
            {
                return ApiResponseDto<AuthResponseDto>.Fail("This account has been locked. Please contact an administrator.");
            }

            if (!IsClientLoginAllowed(user.RoleCode))
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
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Could not sign you in. Please try again.");
        }

        return ApiResponseDto<AuthResponseDto>.Ok(BuildAuthResponse(user), "Google sign-in successful!");
    }

    /// <summary>
    /// The client login page serves Trainee/Coach only.
    /// Admin/Staff sign in on a separate back-office page (built later).
    /// </summary>
    private static bool IsClientLoginAllowed(string? roleCode)
    {
        return string.Equals(roleCode?.Trim(), RoleConstants.Trainee, StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleCode?.Trim(), RoleConstants.Coach, StringComparison.OrdinalIgnoreCase);
    }

    private static User BuildGoogleUser(
        GoogleJsonWebSignature.Payload payload, string normalizedEmail, string googleSub)
    {
        return new User
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
