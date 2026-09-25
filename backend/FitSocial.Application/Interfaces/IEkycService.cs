using FitSocial.Application.DTOs.Coach;

namespace FitSocial.Application.Interfaces;

public interface IEkycService
{
    /// <summary>
    /// Verifies front/back ID card + liveness face via Viettel eKYC (or mock when not configured).
    /// Returns extracted fields that will be stored in CoachEkycVerifications.
    /// </summary>
    Task<(bool Success, string? Error, EkycExtract Result)> VerifyAsync(EkycVerificationDto request, CancellationToken cancellationToken = default);
}

public record EkycExtract(
    string IdCardNumber,
    string FullNameOnCard,
    DateOnly? DateOfBirthOnCard,
    string VerificationStatus,
    string? FailureReason);
