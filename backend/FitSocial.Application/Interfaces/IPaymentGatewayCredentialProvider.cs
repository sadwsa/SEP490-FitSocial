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
/// requires redeploying appsettings.
/// </summary>
public interface IPaymentGatewayCredentialProvider
{
    /// <summary>
    /// Returns the first usable active config among <paramref name="gatewayNames"/>
    /// (in order), or null when none exists or decrypts cleanly.
    /// </summary>
    Task<GatewayCredentials?> GetDbCredentialsAsync(IEnumerable<string> gatewayNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns credentials only when the DB holds exactly one active config
    /// (convenience so a differently-named single gateway still resolves).
    /// </summary>
    Task<GatewayCredentials?> GetSingleActiveCredentialsAsync(CancellationToken cancellationToken = default);
}
