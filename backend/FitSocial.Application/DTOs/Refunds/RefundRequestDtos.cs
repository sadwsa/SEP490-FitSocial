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
    public Guid RequestedBy { get; set; }
    public string? RequestedByName { get; set; }
    public string? RequestedByEmail { get; set; }
    public decimal? RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? Reason { get; set; }
    public List<string> EvidenceUrls { get; set; } = new();
    public string? Status { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? StaffNote { get; set; }
    public string? RefundTransactionRef { get; set; }
    public DateTime? RequestedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
