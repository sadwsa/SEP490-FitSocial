using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Payout
{
    public Guid PayoutId { get; set; }

    public Guid CoachId { get; set; }

    public int PayoutMonth { get; set; }

    public int PayoutYear { get; set; }

    public decimal? TotalGrossAmount { get; set; }

    public decimal? SystemCommissionAmount { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? NetPayoutAmount { get; set; }

    public string? Status { get; set; }

    public Guid? ProcessedBy { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CoachProfile Coach { get; set; } = null!;

    public virtual ICollection<PayoutItem> PayoutItems { get; set; } = new List<PayoutItem>();

    public virtual User? ProcessedByNavigation { get; set; }
}
