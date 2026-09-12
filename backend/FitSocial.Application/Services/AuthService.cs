using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IApplicationDbContext context,
        IOtpService otpService,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _context = context;
        _otpService = otpService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<ApiResponseDto<bool>> SendOtpAsync(SendOtpRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        // Kiểm tra xem email đã tồn tại trong hệ thống chưa
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<bool>.Fail("Email đã được đăng ký trong hệ thống. Vui lòng sử dụng email khác hoặc đăng nhập.");
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose) 
            ? OtpConstants.PurposeRegisterTrainee 
            : request.Purpose;

        // Sinh mã OTP và lưu hash vào bảng OTPLogs
        var otpCode = await _otpService.GenerateOtpAsync(normalizedEmail, purpose);

        // Gửi email chứa mã OTP
        var subject = "[FitSocial] Mã xác thực đăng ký tài khoản";
        var body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;"">
                <div style=""text-align: center; margin-bottom: 20px;"">
                    <h2 style=""color: #FF5722; margin: 0;"">FitSocial 🔥</h2>
                    <p style=""color: #666; margin: 5px 0 0;"">Cộng đồng Thể thao & Thể hình</p>
                </div>
                <div style=""background: #fff3e0; border-left: 4px solid #FF5722; padding: 15px; margin-bottom: 20px;"">
                    <p style=""margin: 0; color: #333; font-size: 16px;"">Xin chào,</p>
                    <p style=""margin: 10px 0 0; color: #555;"">Bạn đang thực hiện đăng ký tài khoản Học viên (Trainee) tại FitSocial. Mã xác thực OTP của bạn là:</p>
                </div>
                <div style=""text-align: center; margin: 30px 0;"">
                    <span style=""font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #FF5722; background: #fbe9e7; padding: 10px 24px; border-radius: 8px; border: 1px dashed #FF5722;"">
                        {otpCode}
                    </span>
                </div>
                <p style=""color: #777; font-size: 14px; text-align: center;"">Mã xác thực có hiệu lực trong <strong>5 phút</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
                <hr style=""border: none; border-top: 1px solid #eee; margin: 25px 0;"" />
                <p style=""color: #999; font-size: 12px; text-align: center; margin: 0;"">Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email.</p>
            </div>";

        await _emailService.SendEmailAsync(normalizedEmail, subject, body);

        return ApiResponseDto<bool>.Ok(true, "Mã xác thực OTP đã được gửi đến email của bạn.");
    }

    public async Task<ApiResponseDto<AuthResponseDto>> RegisterTraineeAsync(RegisterTraineeRequestDto request)
    {
        var normalizedEmail = request.Email.Trim().ToLower();

        if (request.Password != request.ConfirmPassword)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Mật khẩu xác nhận không khớp.");
        }

        // Kiểm tra trùng email
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail);

        if (emailExists)
        {
            return ApiResponseDto<AuthResponseDto>.Fail("Email đã được sử dụng.");
        }

        // Xác minh OTP
        var (otpValid, otpMessage) = await _otpService.ValidateOtpAsync(
            normalizedEmail, 
            request.OtpCode.Trim(), 
            OtpConstants.PurposeRegisterTrainee);

        if (!otpValid)
        {
            return ApiResponseDto<AuthResponseDto>.Fail(otpMessage);
        }

        // Tạo User mới
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // Sinh JWT Token
        var (token, expiresAt) = _jwtTokenGenerator.GenerateToken(newUser);

        var responseData = new AuthResponseDto
        {
            AccessToken = token,
            RefreshToken = Guid.NewGuid().ToString("N"),
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = newUser.UserId,
                FullName = newUser.FullName ?? string.Empty,
                Email = newUser.Email,
                PhoneNumber = newUser.PhoneNumber,
                RoleCode = newUser.RoleCode,
                AvatarUrl = newUser.AvatarUrl
            }
        };

        return ApiResponseDto<AuthResponseDto>.Ok(responseData, "Đăng ký tài khoản thành công!");
    }
}
