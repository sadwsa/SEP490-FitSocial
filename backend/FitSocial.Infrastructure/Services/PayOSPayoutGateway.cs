using FitSocial.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V1.Payouts;

namespace FitSocial.Infrastructure.Services;

/// <summary>
/// UC-23.1: real disbursement via PayOS Payout API (POST /v1/payouts).
/// Requires the payout channel to be activated on the PayOS dashboard
/// (Settings → Profile → Kênh chi) with its own credentials:
/// PayOS:PayoutClientId / PayOS:PayoutApiKey / PayOS:PayoutChecksumKey
/// (falls back to the collection keys PayOS:ClientId/ApiKey/ChecksumKey).
/// Funding comes from the merchant's payout source (e.g. Bao Kim wallet).
/// </summary>
public class PayOSPayoutGateway : IBankTransferGateway
{
    private readonly IConfiguration _configuration;

    /// <summary>Bank code (or 6-digit BIN) → NAPAS BIN used by PayOS ToBin.</summary>
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

    public PayOSPayoutGateway(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private PayOSClient CreateClient()
    {
        var clientId = _configuration["PayOS:PayoutClientId"];
        var apiKey = _configuration["PayOS:PayoutApiKey"];
        var checksumKey = _configuration["PayOS:PayoutChecksumKey"];

        // Fall back to the collection-channel keys if dedicated payout keys are absent.
        clientId ??= _configuration["PayOS:ClientId"];
        apiKey ??= _configuration["PayOS:ApiKey"];
        checksumKey ??= _configuration["PayOS:ChecksumKey"];

        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(checksumKey))
        {
            throw new InvalidOperationException("PayOS payout is not configured on the server.");
        }

        return new PayOSClient(clientId, apiKey, checksumKey);
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

    public async Task<BankTransferResult> TransferAsync(BankTransferRequest request, CancellationToken cancellationToken = default)
    {
        PayOSClient client;
        try
        {
            client = CreateClient();
        }
        catch (Exception ex)
        {
            return new BankTransferResult(false, null, ex.Message);
        }

        var bin = ResolveBin(request.BankCode);
        if (bin == null)
        {
            return new BankTransferResult(false, null,
                $"Unsupported bank code '{request.BankCode}'. Update the coach bank account with a valid code.");
        }

        if (string.IsNullOrWhiteSpace(request.AccountNumber) || request.Amount <= 0)
        {
            return new BankTransferResult(false, null, "Beneficiary account rejected: invalid account number or amount.");
        }

        var referenceId = string.IsNullOrWhiteSpace(request.Reference)
            ? $"FSPO{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}"
            : request.Reference;
        var payoutRequest = new PayoutRequest
        {
            ReferenceId = referenceId,
            Amount = (long)Math.Round(request.Amount),
            Description = request.Description,
            ToBin = bin,
            ToAccountNumber = request.AccountNumber.Trim(),
            Category = null
        };

        Payout payout;
        try
        {
            // Stable idempotency key per payout: staff retries of the same
            // payout record never create a duplicate PayOS order.
            payout = await client.Payouts.CreateAsync(payoutRequest, referenceId);
        }
        catch (Exception ex)
        {
            return new BankTransferResult(false, null, $"PayOS payout rejected: {ex.Message}");
        }

        var tx = payout.Transactions?.FirstOrDefault();
        if (tx == null)
        {
            return new BankTransferResult(false, payout.Id,
                $"PayOS accepted the order ({payout.ApprovalState}) but returned no transaction. Check the PayOS dashboard.");
        }

        if (tx.State == PayoutTransactionState.Succeeded)
        {
            return new BankTransferResult(true, payout.Id, null);
        }

        var reason = !string.IsNullOrWhiteSpace(tx.ErrorMessage)
            ? tx.ErrorMessage
            : $"PayOS transaction state: {tx.State} (approval: {payout.ApprovalState}).";
        return new BankTransferResult(false, payout.Id, reason);
    }
}
