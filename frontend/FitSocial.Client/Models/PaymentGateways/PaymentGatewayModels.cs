namespace FitSocial.Client.Models.PaymentGateways;

/// <summary>
/// UC-36: one row of the admin payment gateway configs list (keys masked).
/// </summary>
public class PaymentGatewayConfig
{
    public Guid GatewayId { get; set; }
    public string? GatewayName { get; set; }
    public string? ClientId { get; set; }
    public string? ClientIdMasked { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiKeyMasked { get; set; }
    public string? ChecksumKey { get; set; }
    public string? ChecksumKeyMasked { get; set; }
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// UC-36.2: payload for admin creating a payment gateway configuration.
/// </summary>
public class CreatePaymentGatewayConfig
{
    public string GatewayName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// UC-36.3: payload for admin updating a payment gateway configuration.
/// Null key fields mean "keep current".
/// </summary>
public class UpdatePaymentGatewayConfig
{
    public string? GatewayName { get; set; }
    public string? ClientId { get; set; }
    public string? ApiKey { get; set; }
    public string? ChecksumKey { get; set; }
    public string? WebhookUrl { get; set; }
    public bool? IsActive { get; set; }
}
