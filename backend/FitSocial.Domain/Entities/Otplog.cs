using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Otplog
{
    public Guid OtpId { get; set; }

    public Guid? UserId { get; set; }

    public string OtpHash { get; set; } = null!;

    public string? Email { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Purpose { get; set; }

    public int? AttemptCount { get; set; }

    public virtual User? User { get; set; }
}
