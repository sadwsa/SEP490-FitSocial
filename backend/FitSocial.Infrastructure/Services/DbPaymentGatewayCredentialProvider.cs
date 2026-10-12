using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Infrastructure.Services;

/// <summary>
/// Reads gateway credentials from the admin-managed PaymentGatewayConfigs
/// table (keys stored AES-encrypted), filtered by channel purpose.
/// Returns null when no usable row exists, letting callers fall back.
/// </summary>
public class DbPaymentGatewayCredentialProvider : IPaymentGatewayCredentialProvider
{
    private readonly IPaymentGatewayConfigRepository _configs;
    private readonly IEncryptionService _encryption;

    public DbPaymentGatewayCredentialProvider(
        IPaymentGatewayConfigRepository configs,
        IEncryptionService encryption)
    {
        _configs = configs;
        _encryption = encryption;
    }

    public Task<GatewayCredentials?> GetCollectCredentialsAsync(CancellationToken cancellationToken = default)
    {
        return GetCredentialsAsync(isPayout: false, preferredNames: ["PayOS"], cancellationToken);
    }

    public Task<GatewayCredentials?> GetPayoutCredentialsAsync(CancellationToken cancellationToken = default)
    {
        return GetCredentialsAsync(isPayout: true, preferredNames: ["PayOS-Payout"], cancellationToken);
    }

    private async Task<GatewayCredentials?> GetCredentialsAsync(
        bool isPayout, string[] preferredNames, CancellationToken cancellationToken)
    {
        List<FitSocial.Domain.Entities.PaymentGatewayConfig> all;
        try
        {
            all = await _configs.GetAllConfigsAsync(cancellationToken);
        }
        catch
        {
            return null;
        }

        var candidates = all
            .Where(c => c.IsActive == true && (c.IsPayout ?? false) == isPayout)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var name in preferredNames)
        {
            var match = candidates.FirstOrDefault(c =>
                string.Equals(c.GatewayName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            var creds = match == null ? null : TryDecrypt(match);
            if (creds != null)
            {
                return creds;
            }
        }

        // Deterministic fallback: most recently updated row of this purpose.
        foreach (var candidate in candidates.OrderByDescending(c => c.UpdatedAt))
        {
            var creds = TryDecrypt(candidate);
            if (creds != null)
            {
                return creds;
            }
        }

        return null;
    }

    private GatewayCredentials? TryDecrypt(FitSocial.Domain.Entities.PaymentGatewayConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ClientId))
        {
            return null;
        }

        string? apiKey;
        string? checksumKey;
        try
        {
            apiKey = _encryption.Decrypt(config.EncryptedApiKey);
            checksumKey = _encryption.Decrypt(config.EncryptedChecksumKey);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
        {
            return null;
        }

        return new GatewayCredentials(
            config.ClientId.Trim(),
            apiKey.Trim(),
            checksumKey.Trim(),
            config.WebhookUrl);
    }
}
