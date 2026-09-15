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
