using FitSocial.Application.DTOs.Coach;

namespace FitSocial.Application.Interfaces;

public interface IEkycService
{
    /// <summary>
    /// Verifies front/back ID card + liveness face via Viettel eKYC (or mock when not configured).
    /// Returns extracted fields that will be stored in CoachEkycVerifications.
    /// </summary>
    Task<(bool Success, string? Error, EkycExtract Result)> VerifyAsync(EkycVerificationDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an ID card number is still available (not used by another coach).
    /// Read-only pre-validation called right after OCR so duplicates surface immediately.
    /// Returns (Available, Error): Error is set when unavailable or input invalid.
    /// </summary>
    Task<(bool Available, string? Error)> CheckIdCardAsync(string idCardNumber, string? email, CancellationToken cancellationToken = default);
}

public record EkycExtract(
    string IdCardNumber,
    string FullNameOnCard,
    DateOnly? DateOfBirthOnCard,
    string VerificationStatus,
    string? FailureReason,
    string? Birthplace = null,
    string? Sex = null,
    string? Address = null,
    string? Province = null,
    string? District = null,
    string? Ward = null,
    string? ProvinceCode = null,
    string? DistrictCode = null,
    string? WardCode = null,
    string? Street = null,
    string? Nationality = null,
    string? Religion = null,
    string? Ethnicity = null,
    string? Expiry = null,
    string? Feature = null,
    string? IssueDate = null,
    string? IssueBy = null,
    string? DocumentType = null,
    decimal? LivenessScore = null,
    decimal? FaceMatchConfidence = null,
    string? RawInformationJson = null);
