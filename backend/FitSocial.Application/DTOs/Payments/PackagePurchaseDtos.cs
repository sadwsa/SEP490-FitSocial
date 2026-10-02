using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Payments;

/// <summary>
/// UC-20: Order summary preview before paying for a training package.
/// </summary>
public class PackagePurchasePreviewDto
{
    public Guid PackageId { get; set; }
    public string PackageTitle { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public decimal PriceVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public Guid CoachId { get; set; }
    public string CoachName { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string OrderType { get; set; } = "PACKAGE";
}

public class CreatePackagePaymentLinkRequestDto
{
    [Required]
    public Guid PackageId { get; set; }
}

/// <summary>
/// UC-20: PayOS VietQR payload for a training package order.
/// Same shape as coach activation link so frontend can reuse the QR UI.
/// </summary>
public class PackagePaymentLinkDto
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public Guid OrderId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
