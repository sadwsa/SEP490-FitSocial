using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Coach;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Payments;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Payment;

public interface IPaymentService
{
    Task<ApiResponse<CoachActivationPreview>> PrepareCoachActivationAsync(RegisterCoachRequest draft);
    Task<ApiResponse<ActivationLink>> CreateActivationLinkAsync(RegisterCoachRequest draft);
    Task<ApiResponse<AuthResponse>> CompleteActivationAsync(long orderCode);
    Task<ApiResponse<bool>> CancelActivationAsync(long orderCode);
    Task<ApiResponse<CoachSubscriptionStatus>> GetCurrentCoachSubscriptionAsync();
    Task<ApiResponse<ActivationLink>> RenewCoachSubscriptionAsync(Guid planId);
    Task<ApiResponse<ActivationLink>> RegenerateCoachActivationAsync();
    Task<ApiResponse<ActivationLink>> RegenerateCoachActivationByEmailAsync(string email);
}

public class PaymentApiService : IPaymentService
{
    private readonly ApiClient _apiClient;

    public PaymentApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<CoachActivationPreview>> PrepareCoachActivationAsync(RegisterCoachRequest draft)
        => _apiClient.PostAsync<RegisterCoachRequest, CoachActivationPreview>("payments/coach-activation/prepare", draft);

    public Task<ApiResponse<ActivationLink>> CreateActivationLinkAsync(RegisterCoachRequest draft)
        => _apiClient.PostAsync<RegisterCoachRequest, ActivationLink>("payments/coach-activation/create-link", draft);

    public Task<ApiResponse<AuthResponse>> CompleteActivationAsync(long orderCode)
        => _apiClient.PostAsync<object, AuthResponse>("payments/coach-activation/complete", new { orderCode });

    public Task<ApiResponse<bool>> CancelActivationAsync(long orderCode)
        => _apiClient.PostAsync<object, bool>("payments/coach-activation/cancel", new { orderCode });

    public Task<ApiResponse<CoachSubscriptionStatus>> GetCurrentCoachSubscriptionAsync()
        => _apiClient.GetAsync<CoachSubscriptionStatus>("payments/coach-subscription/current");

    public Task<ApiResponse<ActivationLink>> RenewCoachSubscriptionAsync(Guid planId)
        => _apiClient.PostAsync<object, ActivationLink>("payments/coach-subscription/renew", new { planId });

    public Task<ApiResponse<ActivationLink>> RegenerateCoachActivationAsync()
        => _apiClient.PostAsync<ActivationLink>("payments/coach-activation/regenerate");

    public Task<ApiResponse<ActivationLink>> RegenerateCoachActivationByEmailAsync(string email)
        => _apiClient.PostAsync<ActivationLink>($"payments/coach-activation/regenerate-by-email?email={Uri.EscapeDataString(email)}");
}