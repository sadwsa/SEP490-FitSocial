namespace FitSocial.Application.Interfaces;

/// <summary>
/// Decrypted credentials of one payment gateway configuration row.
/// </summary>
public record GatewayCredentials(
    string ClientId,
    string ApiKey,
    string ChecksumKey,
    string? WebhookUrl);

/// <summary>
/// Resolves payment gateway credentials from the DB-managed
/// PaymentGatewayConfigs table (admin UI), so key rotation no longer
/// requires redeploying appsettings. Collection (thu tiền) and payout
/// (chi tiền) channels resolve independently by the IsPayout flag.
/// </summary>
public interface IPaymentGatewayCredentialProvider
{
    /// <summary>Active collection channel (IsPayout = false).</summary>
    Task<GatewayCredentials?> GetCollectCredentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active payout channel (IsPayout = true).</summary>
    Task<GatewayCredentials?> GetPayoutCredentialsAsync(CancellationToken cancellationToken = default);
}
