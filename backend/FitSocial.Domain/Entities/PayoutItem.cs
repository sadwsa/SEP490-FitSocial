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

    /// <summary>
    /// Per-line payment state: PENDING until this line's tranche is paid
    /// (individually per package or together via pay-all), then PROCESSED.
    /// </summary>
    public string? Status { get; set; } = "PENDING";

    public DateTime? ProcessedAt { get; set; }

    /// <summary>Bank/gateway reference of the transfer that paid this line.</summary>
    public string? TransactionRef { get; set; }

    /// <summary>
    /// PayOS payout order id once the order is accepted (money may still be
    /// settling). Lets retries reconcile instead of duplicating transfers.
    /// </summary>
    public string? GatewayRef { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Payout Payout { get; set; } = null!;
}
