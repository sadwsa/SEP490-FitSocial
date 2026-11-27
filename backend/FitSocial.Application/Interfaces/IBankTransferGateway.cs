namespace FitSocial.Application.Interfaces;

public record BankTransferRequest(
    string BankCode,
    string AccountNumber,
    string AccountName,
    decimal Amount,
    string Currency,
    string Description,
    string Reference);

public record BankTransferResult(bool Success, string? TransactionRef, string? FailureReason);

public record CreatedTransfer(
    bool Created,
    string? GatewayRef,
    bool Succeeded,
    string? State,
    string? FailureReason,
    string? TransactionRef);

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
