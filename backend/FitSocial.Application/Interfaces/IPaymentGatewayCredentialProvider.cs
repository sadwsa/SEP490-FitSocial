namespace FitSocial.Application.Interfaces;

public record GatewayCredentials(
    string ClientId,
    string ApiKey,
    string ChecksumKey,
    string? WebhookUrl);

public interface IPaymentGatewayCredentialProvider
{
    Task<GatewayCredentials?> GetCollectCredentialsAsync(CancellationToken cancellationToken = default);

    Task<GatewayCredentials?> GetPayoutCredentialsAsync(CancellationToken cancellationToken = default);
}
