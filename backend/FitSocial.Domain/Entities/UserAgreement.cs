using System;

namespace FitSocial.Domain.Entities;

public partial class UserAgreement
{
    public Guid AgreementId { get; set; }

    public Guid UserId { get; set; }

    public Guid TermId { get; set; }

    public string? IpAddress { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual TermsAndPolicy Term { get; set; } = null!;
}
