using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Orders;
using FitSocial.Client.Models.Payments;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Checkout;

/// <summary>
/// UC-20: Purchase Training Package via PayOS VietQR (authenticated).
/// Uses ApiClient so the JWT is attached + silently refreshed.
/// </summary>
public interface ICheckoutService
{
    Task<ApiResponse<PackagePurchasePreview>> PrepareAsync(Guid packageId);
    Task<ApiResponse<PackagePaymentLink>> CreateLinkAsync(Guid packageId);
    Task<ApiResponse<OrderDto>> CompleteAsync(long orderCode);
    Task<ApiResponse<bool>> CancelAsync(long orderCode);
    Task<ApiResponse<OrderDto>> GetOrderByIdAsync(Guid orderId);
    Task<ApiResponse<List<OrderDto>>> GetMyOrdersAsync();
    Task<ApiResponse<List<CoachTraineeGroupDto>>> GetCoachTraineeOrdersAsync();
    Task<ApiResponse<CoachTraineeGroupDto>> GetCoachTraineeOrderDetailAsync(Guid traineeId);
}

public class CheckoutService : ICheckoutService
{
    private readonly ApiClient _apiClient;

    public CheckoutService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<PackagePurchasePreview>> PrepareAsync(Guid packageId)
    {
        return _apiClient.PostAsync<object, PackagePurchasePreview>(
            "payments/package/purchase/prepare", new { packageId });
    }

    public Task<ApiResponse<PackagePaymentLink>> CreateLinkAsync(Guid packageId)
    {
        return _apiClient.PostAsync<object, PackagePaymentLink>(
            "payments/package/purchase/create-link", new { packageId });
    }

    public Task<ApiResponse<OrderDto>> CompleteAsync(long orderCode)
    {
        return _apiClient.PostAsync<object, OrderDto>(
            "payments/package/purchase/complete", new { orderCode });
    }

    public Task<ApiResponse<bool>> CancelAsync(long orderCode)
    {
        return _apiClient.PostAsync<object, bool>(
            "payments/package/purchase/cancel", new { orderCode });
    }

    public Task<ApiResponse<OrderDto>> GetOrderByIdAsync(Guid orderId)
    {
        return _apiClient.GetAsync<OrderDto>($"orders/{orderId}");
    }

    public Task<ApiResponse<List<OrderDto>>> GetMyOrdersAsync()
    {
        return _apiClient.GetAsync<List<OrderDto>>("orders/my-orders");
    }

    public Task<ApiResponse<List<CoachTraineeGroupDto>>> GetCoachTraineeOrdersAsync()
    {
        return _apiClient.GetAsync<List<CoachTraineeGroupDto>>("orders/coach-trainees");
    }

    public Task<ApiResponse<CoachTraineeGroupDto>> GetCoachTraineeOrderDetailAsync(Guid traineeId)
    {
        return _apiClient.GetAsync<CoachTraineeGroupDto>($"orders/coach-trainees/{traineeId}");
    }
}
