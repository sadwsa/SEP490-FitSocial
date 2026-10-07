using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Refunds;

public class CreateRefundRequestDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 1000 characters.")]
    public string Reason { get; set; } = string.Empty;

    public List<string>? EvidenceUrls { get; set; }
}

public class GetRefundRequestsQueryDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; } // PENDING, APPROVED, REJECTED
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SearchTerm { get; set; } // Trainee name/email, package title, order id
}

public class ApproveRefundRequestDto
{
    public decimal? ApprovedAmount { get; set; }

    [StringLength(1000)]
    public string? StaffNote { get; set; }

    [StringLength(100)]
    public string? RefundTransactionRef { get; set; }
}

public class RejectRefundRequestDto
{
    [Required(ErrorMessage = "Rejection note/reason is required.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Rejection note must be between 3 and 1000 characters.")]
    public string StaffNote { get; set; } = string.Empty;
}

public class ProcessRefundRequestDto
{
    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "APPROVED"; // APPROVED, REJECTED

    public decimal? ApprovedAmount { get; set; }

    [StringLength(1000)]
    public string? StaffNote { get; set; }

    [StringLength(100)]
    public string? RefundTransactionRef { get; set; }
}

public class RefundRequestDto
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
    public decimal? ActualPaidAmount { get; set; } // Stored order/payment transaction amount
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

    // Policy & terms snapshot
    public Guid? TermId { get; set; }
    public string? TermVersion { get; set; }
    public string? TermTitle { get; set; }
}
