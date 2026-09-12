using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class CoachBankAccount
{
    public Guid BankId { get; set; }

    public Guid CoachId { get; set; }

    public string? BankName { get; set; }

    public string? BankCode { get; set; }

    public string? AccountName { get; set; }

    public string? AccountNumber { get; set; }

    public string? Branch { get; set; }

    public bool? IsDefault { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;
}
