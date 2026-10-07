using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;

namespace FitSocial.Infrastructure.Services;

/// <summary>
/// VietQR payment gateway via PayOS SDK.
/// Credentials resolve from the admin-managed PaymentGatewayConfigs table
/// first ("PayOS" row, else the single active row), falling back to the
/// PayOS section of appsettings.
/// </summary>
public class PayOSGateway : IPaymentGateway
{
    private readonly IConfiguration _configuration;
    private readonly IPaymentGatewayCredentialProvider _credentials;

    public PayOSGateway(IConfiguration configuration, IPaymentGatewayCredentialProvider? credentials = null)
    {
        _configuration = configuration;
        _credentials = credentials!;
    }

    private async Task<PayOSClient> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        if (_credentials != null)
        {
            var dbCreds = await _credentials.GetDbCredentialsAsync(["PayOS"], cancellationToken)
                ?? await _credentials.GetSingleActiveCredentialsAsync(cancellationToken);
            if (dbCreds != null)
            {
                return new PayOSClient(dbCreds.ClientId, dbCreds.ApiKey, dbCreds.ChecksumKey);
            }
        }

        var clientId = _configuration["PayOS:ClientId"];
        var apiKey = _configuration["PayOS:ApiKey"];
        var checksumKey = _configuration["PayOS:ChecksumKey"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(checksumKey))
        {
            throw new InvalidOperationException("PayOS is not configured on the server.");
        }

        return new PayOSClient(clientId, apiKey, checksumKey);
    }

    public async Task<PaymentLinkInfo> CreatePaymentLinkAsync(
        long orderCode, decimal amount, string description, string returnUrl, string cancelUrl)
    {
        var client = await CreateClientAsync();

        var request = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = (int)amount,
            Description = description,
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl
        };

        var result = await client.PaymentRequests.CreateAsync(request);

        return new PaymentLinkInfo(
            result.CheckoutUrl,
            orderCode,
            result.QrCode ?? string.Empty,
            result.AccountNumber ?? string.Empty,
            result.AccountName ?? string.Empty,
            result.Amount,
            result.Description ?? string.Empty);
    }

    public async Task<GatewayPaymentStatus> GetPaymentStatusAsync(long orderCode)
    {
        var client = await CreateClientAsync();

        var info = await client.PaymentRequests.GetAsync(orderCode);
        var paid = info != null
            && string.Equals(info.Status.ToString(), "PAID", StringComparison.OrdinalIgnoreCase);

        return new GatewayPaymentStatus(paid, (long)(info?.Amount ?? 0), info?.Transactions?.FirstOrDefault()?.Reference);
    }
}
