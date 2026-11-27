using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V1.Payouts;

namespace FitSocial.Infrastructure.Services;

public class PayOSPayoutGateway : IBankTransferGateway
{
    private readonly IConfiguration _configuration;
    private readonly IPaymentGatewayCredentialProvider _credentials;

    private static readonly Dictionary<string, string> BankBins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MB"] = "970422",
        ["VCB"] = "970436", ["VIETCOMBANK"] = "970436",
        ["TCB"] = "970407", ["TECHCOMBANK"] = "970407",
        ["CTG"] = "970415", ["VIETINBANK"] = "970415",
        ["BIDV"] = "970418",
        ["AGRIBANK"] = "970405", ["AGB"] = "970405",
        ["ACB"] = "970416",
        ["VPB"] = "970432", ["VPBANK"] = "970432",
        ["TPB"] = "970423", ["TPBANK"] = "970423",
        ["SHB"] = "970443",
        ["HDB"] = "970437", ["HDBANK"] = "970437",
        ["MSB"] = "970426",
        ["VIB"] = "970441",
        ["OCB"] = "970448",
        ["EXIMBANK"] = "970431", ["EIB"] = "970431",
        ["SACOMBANK"] = "970403", ["STB"] = "970403",
        ["SEABANK"] = "970440", ["SSB"] = "970440",
        ["LPB"] = "970449", ["LIENVIET"] = "970449",
        ["BAB"] = "970409", ["BACABANK"] = "970409",
        ["NCB"] = "970419",
        ["KLB"] = "970452", ["KIENLONG"] = "970452",
        ["VIETBANK"] = "970433", ["VBB"] = "970433",
        ["BVBANK"] = "970454", ["BVBB"] = "970454",
        ["PVCOMBANK"] = "970412", ["PVCB"] = "970412",
        ["SAIGONBANK"] = "970400", ["SGB"] = "970400",
        ["DONGABANK"] = "970406", ["DAB"] = "970406",
        ["NAMABANK"] = "970428", ["NAB"] = "970428",
        ["PGBANK"] = "970430", ["PGB"] = "970430",
        ["VIETCAPITAL"] = "970456", ["VCBANK"] = "970456",
        ["KIENLONG"] = "970452",
        ["CAKE"] = "970443",
    };

    public PayOSPayoutGateway(IConfiguration configuration, IPaymentGatewayCredentialProvider? credentials = null)
    {
        _configuration = configuration;
        _credentials = credentials!;
    }

    private static HttpClient CreateIPv4HttpClient()
    {
        var handler = new System.Net.Http.SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var entry = await System.Net.Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
                var ipv4 = entry.AddressList.FirstOrDefault(a =>
                    a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 == null)
                {
                    throw new InvalidOperationException("PayOS API host has no IPv4 address.");
                }
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new System.Net.IPEndPoint(ipv4, context.DnsEndPoint.Port), cancellationToken);
                    return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
        return new HttpClient(handler);
    }

    private async Task<(PayOSClient Client, HttpClient Http)> CreateClientAsync(CancellationToken cancellationToken = default)
    {
        string clientId;
        string apiKey;
        string checksumKey;

        if (_credentials != null)
        {
            var dbCreds = await _credentials.GetPayoutCredentialsAsync(cancellationToken);
            if (dbCreds != null)
            {
                var http = CreateIPv4HttpClient();
                return (new PayOSClient(new PayOSOptions
                {
                    ClientId = dbCreds.ClientId,
                    ApiKey = dbCreds.ApiKey,
                    ChecksumKey = dbCreds.ChecksumKey,
                    HttpClient = http
                }), http);
            }
        }

        clientId = _configuration["PayOS:PayoutClientId"];
        apiKey = _configuration["PayOS:PayoutApiKey"];
        checksumKey = _configuration["PayOS:PayoutChecksumKey"];

        clientId ??= _configuration["PayOS:ClientId"];
        apiKey ??= _configuration["PayOS:ApiKey"];
        checksumKey ??= _configuration["PayOS:ChecksumKey"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(checksumKey))
        {
            throw new InvalidOperationException("PayOS payout is not configured on the server.");
        }

        var fallbackHttp = CreateIPv4HttpClient();
        return (new PayOSClient(new PayOSOptions
        {
            ClientId = clientId,
            ApiKey = apiKey,
            ChecksumKey = checksumKey,
            HttpClient = fallbackHttp
        }), fallbackHttp);
    }

    public static string? ResolveBin(string? bankCode)
    {
        if (string.IsNullOrWhiteSpace(bankCode))
        {
            return null;
        }

        var code = bankCode.Trim();
        if (code.Length == 6 && code.All(char.IsDigit))
        {
            return code;
        }

        return BankBins.TryGetValue(code, out var bin) ? bin : null;
    }

    public async Task<CreatedTransfer> CreateTransferAsync(BankTransferRequest request, CancellationToken cancellationToken = default)
    {
        PayOSClient client;
        HttpClient http;
        try
        {
            (client, http) = await CreateClientAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return new CreatedTransfer(false, null, false, null, ex.Message, null);
        }

        try
        {
            return await CreateTransferCoreAsync(client, request, cancellationToken);
        }
        finally
        {
            client.Dispose();
            http.Dispose();
        }
    }

    public async Task<BankTransferStatus?> GetTransferStatusAsync(string gatewayPayoutId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayPayoutId))
        {
            return null;
        }

        PayOSClient client;
        HttpClient http;
        try
        {
            (client, http) = await CreateClientAsync(cancellationToken);
        }
        catch
        {
            return null;
        }

        try
        {
            var payout = await client.Payouts.GetAsync(gatewayPayoutId.Trim());
            return MapStatus(payout);
        }
        catch
        {
            return null;
        }
        finally
        {
            client.Dispose();
            http.Dispose();
        }
    }

    public async Task<BankTransferStatus?> FindTransferByReferenceAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return null;
        }

        PayOSClient client;
        HttpClient http;
        try
        {
            (client, http) = await CreateClientAsync(cancellationToken);
        }
        catch
        {
            return null;
        }

        try
        {
            var page = await client.Payouts.ListAsync(
                new GetPayoutListParam { ReferenceId = referenceId.Trim(), Limit = 5 });
            var payout = page?.Data?.FirstOrDefault(p => string.Equals(p.ReferenceId, referenceId.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? page?.Data?.FirstOrDefault();
            return payout == null ? null : MapStatus(payout);
        }
        catch
        {
            return null;
        }
        finally
        {
            client.Dispose();
            http.Dispose();
        }
    }

    private static BankTransferStatus MapStatus(Payout payout)
    {
        var tx = payout.Transactions?.FirstOrDefault();
        var succeeded = tx != null && tx.State == PayoutTransactionState.Succeeded;
        var state = tx?.State.ToString() ?? payout.ApprovalState.ToString();
        string? failure = null;
        if (!succeeded && tx != null && (tx.State == PayoutTransactionState.Failed
            || tx.State == PayoutTransactionState.Cancelled
            || tx.State == PayoutTransactionState.Reversed))
        {
            failure = !string.IsNullOrWhiteSpace(tx.ErrorMessage) ? tx.ErrorMessage : $"PayOS transaction {tx.State}.";
        }
        return new BankTransferStatus(true, succeeded, state, failure, payout.Id, payout.Id);
    }

    private async Task<CreatedTransfer> CreateTransferCoreAsync(
        PayOSClient client, BankTransferRequest request, CancellationToken cancellationToken)
    {
        var bin = ResolveBin(request.BankCode);
        if (bin == null)
        {
            return new CreatedTransfer(false, null, false, null,
                $"Unsupported bank code '{request.BankCode}'. Update the coach bank account with a valid code.", null);
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber) || request.Amount <= 0)
        {
            return new CreatedTransfer(false, null, false, null,
                "Beneficiary account rejected: invalid account number or amount.", null);
        }

        var referenceId = string.IsNullOrWhiteSpace(request.Reference)
            ? $"FSPO{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}"
            : request.Reference;
        var amount = (long)Math.Round(request.Amount);
        var payoutRequest = new PayoutRequest
        {
            ReferenceId = referenceId,
            Amount = amount,
            Description = request.Description,
            ToBin = bin,
            ToAccountNumber = request.AccountNumber.Trim(),
            Category = null
        };

        var balanceError = await CheckBalanceAsync(client, amount, cancellationToken);
        if (balanceError != null)
        {
            return new CreatedTransfer(false, null, false, null, balanceError, null);
        }

        Payout payout;
        try
        {
            payout = await client.Payouts.CreateAsync(payoutRequest, referenceId);
        }
        catch (Exception ex)
        {
            return new CreatedTransfer(false, null, false, null, MapPayOSException(ex, amount), null);
        }

        var tx = payout.Transactions?.FirstOrDefault();
        var succeeded = tx != null && tx.State == PayoutTransactionState.Succeeded;
        return new CreatedTransfer(
            true,
            payout.Id,
            succeeded,
            tx?.State.ToString() ?? payout.ApprovalState.ToString(),
            succeeded ? null : (!string.IsNullOrWhiteSpace(tx?.ErrorMessage) ? tx.ErrorMessage : null),
            payout.Id);
    }

    private static async Task<string?> CheckBalanceAsync(
        PayOSClient client, long amount, CancellationToken cancellationToken)
    {
        try
        {
            var info = await client.PayoutsAccount.GetBalanceAsync();
            if (info == null)
            {
                return null;
            }

            if (decimal.TryParse(
                    info.Balance?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var balance) && balance < amount)
            {
                return $"Insufficient payout source balance ({balance:N0} VND available, {amount:N0} VND needed). Top up the payout wallet on the PayOS dashboard and retry.";
            }
        }
        catch
        {
            // Fall through: let the create call report the real problem.
        }

        return null;
    }

    private static string MapPayOSException(Exception ex, long amount)
    {
        return ex switch
        {
            PayOS.Exceptions.UnauthorizedException =>
                "PayOS authentication failed (401): the payout API key is missing/inactive or the payout channel (Kênh chi) is not activated for these credentials.",
            PayOS.Exceptions.ForbiddenException =>
                "PayOS refused the payout order (403): this credential has no payout permission. Check that the payout channel (Kênh chi) is approved AND that this server's public IP is whitelisted under 'Quản lý IP cho kênh' on the PayOS dashboard.",
            PayOS.Exceptions.BadRequestException =>
                $"PayOS rejected the payout data (400) for {amount:N0} VND. Common causes: amount below the minimum per transaction, insufficient wallet balance, invalid beneficiary account/bank, or invalid description. Check the payout wallet balance and channel limits on the PayOS dashboard, then retry.",
            PayOS.Exceptions.TooManyRequestsException =>
                "PayOS rate limit hit (429). Wait a moment and retry.",
            PayOS.Exceptions.ConnectionTimeoutException or PayOS.Exceptions.ConnectionException =>
                $"Could not reach PayOS ({ex.Message}). Check network and retry.",
            PayOS.Exceptions.ApiException api when !string.IsNullOrWhiteSpace(api.ErrorDescription) && api.ErrorDescription != "API request failed" =>
                $"PayOS payout rejected: {api.ErrorDescription}",
            _ => $"PayOS payout rejected: {ex.Message}",
        };
    }
}
