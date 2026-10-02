using System;
using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.PaymentGateway;

public class PaymentGatewayConfigDto
{
    public Guid GatewayId { get; set; }
    public string? GatewayName { get; set; }
    public string? ClientId { get; set; }
    public string? ApiKeyMasked { get; set; }
    public string? ChecksumKeyMasked { get; set; }
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreatePaymentGatewayConfigDto
{
    [Required]
    [StringLength(50)]
    public string GatewayName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string ChecksumKey { get; set; } = string.Empty;

    [StringLength(2048)]
    public string? WebhookUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdatePaymentGatewayConfigDto
{
    [StringLength(50)]
    public string? GatewayName { get; set; }

    [StringLength(255)]
    public string? ClientId { get; set; }

    [StringLength(500)]
    public string? ApiKey { get; set; }

    [StringLength(500)]
    public string? ChecksumKey { get; set; }

    [StringLength(2048)]
    public string? WebhookUrl { get; set; }

    public bool? IsActive { get; set; }
}
