using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.PaymentGateways;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.PaymentGateways;

/// <summary>
/// UC-36/36.1/36.2: Admin management of payment gateway configurations.
/// </summary>
public interface IPaymentGatewayService
{
    Task<ApiResponse<List<PaymentGatewayConfig>>> GetAllAsync();
    Task<ApiResponse<PaymentGatewayConfig>> CreateAsync(CreatePaymentGatewayConfig request);
    Task<ApiResponse<bool>> ActivateAsync(Guid gatewayId);
    Task<ApiResponse<bool>> DeactivateAsync(Guid gatewayId);
}

public class PaymentGatewayApiService : IPaymentGatewayService
{
    private readonly ApiClient _apiClient;

    public PaymentGatewayApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<List<PaymentGatewayConfig>>> GetAllAsync()
    {
        return _apiClient.GetAsync<List<PaymentGatewayConfig>>("paymentgatewayconfigs");
    }

    public Task<ApiResponse<PaymentGatewayConfig>> CreateAsync(CreatePaymentGatewayConfig request)
    {
        return _apiClient.PostAsync<CreatePaymentGatewayConfig, PaymentGatewayConfig>("paymentgatewayconfigs", request);
    }

    public Task<ApiResponse<bool>> ActivateAsync(Guid gatewayId)
    {
        return _apiClient.PutAsync<bool>($"paymentgatewayconfigs/{gatewayId}/activate");
    }

    public Task<ApiResponse<bool>> DeactivateAsync(Guid gatewayId)
    {
        return _apiClient.PutAsync<bool>($"paymentgatewayconfigs/{gatewayId}/deactivate");
    }
}
