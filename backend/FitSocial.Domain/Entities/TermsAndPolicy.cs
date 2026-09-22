using System;

namespace FitSocial.Domain.Entities;

public partial class TermsAndPolicy
{
    public Guid TermId { get; set; }

    public string Version { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime EffectiveDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<UserAgreement> UserAgreements { get; set; } = new List<UserAgreement>();
}
