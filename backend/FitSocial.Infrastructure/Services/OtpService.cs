using System.Security.Cryptography;
using System.Text;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IApplicationDbContext _context;

    public OtpService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateOtpAsync(string email, string purpose)
    {
        var normalizedEmail = email.Trim().ToLower();

        // 1. Sinh ngẫu nhiên mã OTP 6 số (100000 - 999999)
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpHash = ComputeSha256Hash(otpCode);

        // 2. Hủy các mã OTP cũ chưa xác thực cho email và mục đích này
        var oldOtps = await _context.Otplogs
            .Where(o => o.Email == normalizedEmail && o.Purpose == purpose && o.VerifiedAt == null)
            .ToListAsync();

        if (oldOtps.Any())
        {
            _context.Otplogs.RemoveRange(oldOtps);
        }

        // 3. Tạo bản ghi mới trong bảng OTPLogs
        var newOtp = new Otplog
        {
            OtpId = Guid.NewGuid(),
            Email = normalizedEmail,
            OtpHash = otpHash,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(OtpConstants.ExpiryMinutes),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.Otplogs.Add(newOtp);
        await _context.SaveChangesAsync();

        return otpCode;
    }

    public async Task<(bool Success, string Message)> ValidateOtpAsync(string email, string otpCode, string purpose)
    {
        var normalizedEmail = email.Trim().ToLower();

        var activeOtp = await _context.Otplogs
            .Where(o => o.Email == normalizedEmail && o.Purpose == purpose && o.VerifiedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (activeOtp == null)
        {
            return (false, "Không tìm thấy yêu cầu mã OTP hoặc mã đã được xác thực. Vui lòng nhấn gửi lại mã.");
        }

        if (DateTime.UtcNow > activeOtp.ExpiresAt)
        {
            return (false, "Mã OTP đã hết thời hạn sử dụng. Vui lòng nhấn gửi lại mã mới.");
        }

        if ((activeOtp.AttemptCount ?? 0) >= OtpConstants.MaxAttempts)
        {
            return (false, "Bạn đã nhập sai mã OTP vượt quá số lần cho phép (5 lần). Vui lòng gửi lại mã mới.");
        }

        var inputHash = ComputeSha256Hash(otpCode);
        if (inputHash != activeOtp.OtpHash)
        {
            activeOtp.AttemptCount = (activeOtp.AttemptCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            var remainingAttempts = OtpConstants.MaxAttempts - activeOtp.AttemptCount.Value;
            return (false, $"Mã OTP không chính xác. Bạn còn {remainingAttempts} lần thử.");
        }

        // Đánh dấu đã xác thực
        activeOtp.VerifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (true, "Xác thực OTP thành công.");
    }

    private static string ComputeSha256Hash(string rawData)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
