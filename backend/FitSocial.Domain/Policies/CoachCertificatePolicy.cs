using System;
using System.Collections.Generic;
using System.Linq;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Policies;

/// <summary>
/// Domain policy evaluating validity and expiration of coach certificates.
/// </summary>
public static class CoachCertificatePolicy
{
    // Indochina Time (UTC+7) as standard business timezone offset for FitSocial
    public static readonly TimeSpan BusinessTimeZoneOffset = TimeSpan.FromHours(7);

    /// <summary>
    /// Gets current business date in the business timezone (UTC+7).
    /// </summary>
    public static DateOnly GetBusinessToday()
    {
        var businessNow = DateTime.UtcNow.Add(BusinessTimeZoneOffset);
        return DateOnly.FromDateTime(businessNow);
    }

    /// <summary>
    /// Checks whether a certificate is expired based on actual ExpiryDate.
    /// If ExpiryDate is in the past compared to the business date, it is considered expired.
    /// </summary>
    public static bool IsCertificateExpired(CoachCertificate? cert, DateOnly? businessToday = null)
    {
        if (cert == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return cert.ExpiryDate.HasValue && cert.ExpiryDate.Value < today;
    }

    /// <summary>
    /// Checks whether a certificate is valid and can be displayed publicly.
    /// Rules:
    /// 1. VerificationStatus must be APPROVED.
    /// 2. ExpiryDate must be present and on or after the business date (not expired).
    /// Note: If ExpiryDate is missing or indeterminate, it is not assumed to be valid.
    /// </summary>
    public static bool IsCertificateValid(CoachCertificate? cert, DateOnly? businessToday = null)
    {
        if (cert == null) return false;
        if (!string.Equals(cert.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Must have an explicit ExpiryDate and not be expired
        if (!cert.ExpiryDate.HasValue)
        {
            return false;
        }

        var today = businessToday ?? GetBusinessToday();
        return cert.ExpiryDate.Value >= today;
    }

    /// <summary>
    /// Determines if the coach has at least one expired certificate.
    /// </summary>
    public static bool HasExpiredCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return certificates.Any(c => IsCertificateExpired(c, today));
    }

    /// <summary>
    /// Determines if the coach has at least one valid, approved, unexpired certificate.
    /// </summary>
    public static bool HasValidCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return certificates.Any(c => IsCertificateValid(c, today));
    }

    /// <summary>
    /// Returns only certificates that are approved and not expired.
    /// </summary>
    public static List<CoachCertificate> GetValidCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return new List<CoachCertificate>();
        var today = businessToday ?? GetBusinessToday();
        return certificates.Where(c => IsCertificateValid(c, today)).ToList();
    }
}
