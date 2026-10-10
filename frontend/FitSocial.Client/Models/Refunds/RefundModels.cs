using System;
using System.Collections.Generic;

namespace FitSocial.Client.Models.Refunds;

public class RefundRequestItem
{
    public Guid RefundRequestId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? PaymentId { get; set; }

    // Trainee information
    public Guid RequestedBy { get; set; }
    public string? RequestedByName { get; set; }
    public string? RequestedByEmail { get; set; }
    public string? RequestedByAvatarUrl { get; set; }

    // Training Package information snapshot
    public Guid? PackageId { get; set; }
    public string? PackageTitle { get; set; }
    public int? PackageDurationDays { get; set; }
    public decimal? PackageSnapshotPrice { get; set; }
    public string? CoachName { get; set; }
    public Guid? CoachId { get; set; }

    // Financial snapshot
    public decimal? ActualPaidAmount { get; set; }
    public decimal? RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }

    // Refund details
    public string? Reason { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public string? Status { get; set; } // PENDING, APPROVED, REJECTED

    // Admin/Staff review information
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? StaffNote { get; set; }
    public string? RefundTransactionRef { get; set; }

    // Timestamps
    public DateTime? PurchaseDate { get; set; }
    public DateTime? RequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Policy snapshot
    public Guid? TermId { get; set; }
    public string? TermVersion { get; set; }
    public string? TermTitle { get; set; }
}

public class GetRefundRequestsQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; }
}

public class ApproveRefundRequest
{
    public decimal? ApprovedAmount { get; set; }
    public string? StaffNote { get; set; }
    public string? RefundTransactionRef { get; set; }
}

public class RejectRefundRequest
{
    public string StaffNote { get; set; } = string.Empty;
}
