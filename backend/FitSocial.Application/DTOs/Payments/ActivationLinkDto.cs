namespace FitSocial.Application.DTOs.Payments;

public class ActivationLinkDto
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public long OrderCode { get; set; }
    public Guid OrderId { get; set; }

    /// <summary>VietQR content (render as QR image on the client).</summary>
    public string QrCode { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
