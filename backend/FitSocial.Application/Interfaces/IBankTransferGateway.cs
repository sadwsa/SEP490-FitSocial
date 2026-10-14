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

public record BankTransferResult(bool Success, string? TransactionRef, string? FailureReason);

/// <summary>
/// Result of creating a payout order (money may still be settling).
/// </summary>
public record CreatedTransfer(
    bool Created,
    string? GatewayRef,
    bool Succeeded,
    string? State,
    string? FailureReason,
    string? TransactionRef);

/// <summary>
/// Live state of a payout order previously accepted by the gateway.
/// </summary>
public record BankTransferStatus(
    bool Found,
    bool Succeeded,
    string? State,
    string? FailureReason,
    string? GatewayRef,
    string? TransactionRef);

public interface IBankTransferGateway
{
    Task<CreatedTransfer> CreateTransferAsync(BankTransferRequest request, CancellationToken cancellationToken = default);

    Task<BankTransferStatus?> GetTransferStatusAsync(string gatewayPayoutId, CancellationToken cancellationToken = default);

    Task<BankTransferStatus?> FindTransferByReferenceAsync(string referenceId, CancellationToken cancellationToken = default);
}
