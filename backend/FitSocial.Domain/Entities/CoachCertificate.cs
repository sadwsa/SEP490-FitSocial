using System;

namespace FitSocial.Domain.Entities;

public partial class CoachCertificate
{
    public Guid CertificateId { get; set; }

    public Guid CoachId { get; set; }

    public string? CertificateName { get; set; }

    public string? CertificateUrl { get; set; }

    public string? IssuedBy { get; set; }

    public DateOnly? IssuedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public string? VerificationStatus { get; set; }

    public Guid? VerifiedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public string? RejectedReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual User? VerifiedByNavigation { get; set; }
}
