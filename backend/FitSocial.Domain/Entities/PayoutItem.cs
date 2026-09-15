using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class PayoutItem
{
    public Guid PayoutItemId { get; set; }

    public Guid PayoutId { get; set; }

    public Guid OrderId { get; set; }

    public decimal? GrossAmount { get; set; }

    public decimal? CommissionRate { get; set; }

    public decimal? CommissionAmount { get; set; }

    public decimal? RefundAdjustment { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? NetAmount { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Payout Payout { get; set; } = null!;
}
