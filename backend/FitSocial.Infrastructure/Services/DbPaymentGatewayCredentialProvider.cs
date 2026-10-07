using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Infrastructure.Services;

/// <summary>
/// Reads gateway credentials from the admin-managed PaymentGatewayConfigs
/// table (keys stored AES-encrypted). Returns null when no named row exists
/// or decrypts to empty values, letting callers fall back to appsettings.
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

    public async Task<GatewayCredentials?> GetDbCredentialsAsync(IEnumerable<string> gatewayNames, CancellationToken cancellationToken = default)
    {
        foreach (var name in gatewayNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            FitSocial.Domain.Entities.PaymentGatewayConfig? config;
            try
            {
                config = await _configs.GetActiveByNameAsync(name, cancellationToken);
            }
            catch
            {
                continue;
            }

            if (config == null)
            {
                continue;
            }

            var creds = TryDecrypt(config);
            if (creds != null)
            {
                return creds;
            }
        }

        return null;
    }

    public async Task<GatewayCredentials?> GetSingleActiveCredentialsAsync(CancellationToken cancellationToken = default)
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

        var actives = all.Where(c => c.IsActive == true).ToList();
        if (actives.Count != 1)
        {
            return null;
        }

        return TryDecrypt(actives[0]);
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
