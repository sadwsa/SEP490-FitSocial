using System;

namespace FitSocial.Domain.Entities;

public partial class CoachCertificate
{
    public Guid CertificateId { get; set; }

    public Guid CoachId { get; set; }

    public string? CertificateName { get; set; }

    public string CertificateUrl { get; set; } = null!;

    public DateOnly? IssuedDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;
}
