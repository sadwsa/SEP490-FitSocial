using System;
using System.Collections.Generic;

namespace FitSocial.Domain.Entities;

public partial class PaymentGatewayConfig
{
    public Guid GatewayId { get; set; }

    public string? GatewayName { get; set; }

    public string? ClientId { get; set; }

    public byte[]? EncryptedApiKey { get; set; }

    public byte[]? EncryptedChecksumKey { get; set; }

    public string? WebhookUrl { get; set; }

    public bool? IsActive { get; set; } = true;

    public Guid? UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
