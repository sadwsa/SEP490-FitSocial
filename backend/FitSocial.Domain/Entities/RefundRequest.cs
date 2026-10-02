using System;

namespace FitSocial.Domain.Entities;

public partial class RefundRequest
{
    public Guid RefundRequestId { get; set; }

    public Guid OrderId { get; set; }

    public Guid? PaymentId { get; set; }

    public Guid RequestedBy { get; set; }

    public Guid? TermId { get; set; }

    public decimal? RequestedAmount { get; set; }

    public decimal? ApprovedAmount { get; set; }

    public string? Reason { get; set; }

    public string? EvidenceUrls { get; set; }

    public string? Status { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? StaffNote { get; set; }

    public string? RefundTransactionRef { get; set; }

    public DateTime? RequestedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Payment? Payment { get; set; }

    public virtual User RequestedByNavigation { get; set; } = null!;

    public virtual TermsAndPolicy? Term { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }
}
