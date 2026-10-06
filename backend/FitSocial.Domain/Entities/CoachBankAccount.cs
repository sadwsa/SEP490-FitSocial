using System;

namespace FitSocial.Domain.Entities;

public partial class CoachBankAccount
{
    public Guid BankId { get; set; }

    public Guid CoachId { get; set; }

    public string? BankName { get; set; }

    public string? BankCode { get; set; }

    public string? AccountName { get; set; }

    public byte[]? EncryptedAccountNumber { get; set; }

    public string? Branch { get; set; }

    public bool? IsDefault { get; set; } = false;

    /// <summary>
    /// UC-36.1: only active accounts can receive payouts.
    /// </summary>
    public bool? IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;
}
