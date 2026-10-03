namespace FitSocial.Application.Interfaces;

/// <summary>
/// UC-23.1: banking gateway used to disburse coach payouts.
/// </summary>
public record BankTransferRequest(
    string BankCode,
    string AccountNumber,
    string AccountName,
    decimal Amount,
    string Currency,
    string Description,
    string Reference);

public record BankTransferResult(
    bool Success,
    string? TransactionRef,
    string? FailureReason);

public interface IBankTransferGateway
{
    Task<BankTransferResult> TransferAsync(BankTransferRequest request, CancellationToken cancellationToken = default);
}
