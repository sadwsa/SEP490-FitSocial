using System.Security.Cryptography;
using System.Text;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IOtpLogRepository _otpLogs;
    private readonly IUnitOfWork _unitOfWork;

    public OtpService(IOtpLogRepository otpLogs, IUnitOfWork unitOfWork)
    {
        _otpLogs = otpLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GenerateOtpAsync(string email, string purpose)
    {
        var normalizedEmail = email.Trim().ToLower();

        // 1. Generate a random 6-digit OTP code (100000 - 999999)
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otpHash = ComputeSha256Hash(otpCode);

        // 2. Invalidate old unverified OTPs for this email and purpose
        var oldOtps = await _otpLogs.ListUnverifiedAsync(normalizedEmail, purpose);

        if (oldOtps.Any())
        {
            _otpLogs.RemoveRange(oldOtps);
        }

        // 3. Create a new record in OTPLogs
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

        await _otpLogs.AddAsync(newOtp);
        await _unitOfWork.SaveChangesAsync();

        return otpCode;
    }

    public async Task<(bool Success, string Message)> ValidateOtpAsync(string email, string otpCode, string purpose)
    {
        var normalizedEmail = email.Trim().ToLower();

        var activeOtp = await _otpLogs.FindLatestUnverifiedAsync(normalizedEmail, purpose);

        if (activeOtp == null)
        {
            return (false, "No OTP request found or the code has already been verified. Please resend the code.");
        }

        if (DateTime.UtcNow > activeOtp.ExpiresAt)
        {
            return (false, "The OTP code has expired. Please request a new code.");
        }

        if ((activeOtp.AttemptCount ?? 0) >= OtpConstants.MaxAttempts)
        {
            return (false, "You have exceeded the maximum OTP attempts (5 times). Please request a new code.");
        }

        var inputHash = ComputeSha256Hash(otpCode);
        if (inputHash != activeOtp.OtpHash)
        {
            activeOtp.AttemptCount = (activeOtp.AttemptCount ?? 0) + 1;
            await _unitOfWork.SaveChangesAsync();

            var remainingAttempts = OtpConstants.MaxAttempts - activeOtp.AttemptCount.Value;
            return (false, $"Incorrect OTP code. You have {remainingAttempts} attempts left.");
        }

        // Mark as verified
        activeOtp.VerifiedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return (true, "OTP verified successfully.");
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
