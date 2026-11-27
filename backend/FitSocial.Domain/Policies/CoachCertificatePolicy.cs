using System;
using System.Collections.Generic;
using System.Linq;
using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Policies;

public static class CoachCertificatePolicy
{
    public static readonly TimeSpan BusinessTimeZoneOffset = TimeSpan.FromHours(7);

    public static DateOnly GetBusinessToday()
    {
        var businessNow = DateTime.UtcNow.Add(BusinessTimeZoneOffset);
        return DateOnly.FromDateTime(businessNow);
    }

    public static bool IsCertificateExpired(CoachCertificate? cert, DateOnly? businessToday = null)
    {
        if (cert == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return cert.ExpiryDate.HasValue && cert.ExpiryDate.Value < today;
    }

    public static bool IsCertificateValid(CoachCertificate? cert, DateOnly? businessToday = null)
    {
        if (cert == null) return false;
        if (!string.Equals(cert.VerificationStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var today = businessToday ?? GetBusinessToday();
        return !cert.ExpiryDate.HasValue || cert.ExpiryDate.Value >= today;
    }

    public static bool HasExpiredCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return certificates.Any(c => IsCertificateExpired(c, today));
    }

    public static bool HasValidCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return false;
        var today = businessToday ?? GetBusinessToday();
        return certificates.Any(c => IsCertificateValid(c, today));
    }

    public static List<CoachCertificate> GetValidCertificates(IEnumerable<CoachCertificate>? certificates, DateOnly? businessToday = null)
    {
        if (certificates == null) return new List<CoachCertificate>();
        var today = businessToday ?? GetBusinessToday();
        return certificates.Where(c => IsCertificateValid(c, today)).ToList();
    }
}
