using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid OrderId { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? Method { get; set; }

    public string? GatewayId { get; set; }

    public string? TransactionRef { get; set; }

    public string? GatewayTransactionId { get; set; }

    public string? Status { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
