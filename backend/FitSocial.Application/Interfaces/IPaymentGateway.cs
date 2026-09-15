namespace FitSocial.Application.Interfaces;

public record PaymentLinkInfo(
    string CheckoutUrl,
    long OrderCode,
    string QrCode,
    string AccountNumber,
    string AccountName,
    long Amount,
    string Description);

public record GatewayPaymentStatus(bool IsPaid, long Amount, string? Reference);

public interface IPaymentGateway
{
    Task<PaymentLinkInfo> CreatePaymentLinkAsync(
        long orderCode, decimal amount, string description, string returnUrl, string cancelUrl);

    Task<GatewayPaymentStatus> GetPaymentStatusAsync(long orderCode);
}
