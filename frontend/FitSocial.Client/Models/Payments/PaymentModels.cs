namespace FitSocial.Client.Models.Payments;

public class CoachActivationPreview
{
    public string Email { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string OrderType { get; set; } = string.Empty;
}

public class ActivationLink
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

// UC-20: Purchase Training Package
public class PackagePurchasePreview
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
    public string OrderType { get; set; } = string.Empty;
}

public class PackagePaymentLink
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
