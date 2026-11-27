namespace FitSocial.Client.Models.PaymentGateways;

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
    public bool IsPayout { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePaymentGatewayConfig
{
    public string GatewayName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPayout { get; set; } = false;
}

public class UpdatePaymentGatewayConfig
{
    public string? GatewayName { get; set; }
    public string? ClientId { get; set; }
    public string? ApiKey { get; set; }
    public string? ChecksumKey { get; set; }
    public string? WebhookUrl { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsPayout { get; set; }
}
