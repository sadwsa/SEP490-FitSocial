using System.Net.Http.Json;
using FitSocial.Client.Models.Auth;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Payments;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Payment;

/// <summary>
/// Holds the coach registration draft while the user pays on /coach-payment.
/// Account is only created (register-coach) after successful payment.
/// </summary>
public class PendingCoachRegistration
{
    public RegisterRequest? Draft { get; set; }
    public List<Guid> SportIds { get; set; } = new();
    public List<Guid> SpecialtyIds { get; set; } = new();
    public List<string> SpecialtyNames { get; set; } = new();

    public bool HasDraft => Draft != null;

    public void Clear()
    {
        Draft = null;
        SportIds = new List<Guid>();
        SpecialtyIds = new List<Guid>();
        SpecialtyNames = new List<string>();
    }
}

public interface IPaymentService
{
    Task<ApiResponse<CoachActivationPreview>> PrepareCoachActivationAsync(RegisterRequest draft);
    Task<ApiResponse<ActivationLink>> CreateActivationLinkAsync(RegisterRequest draft);
    Task<ApiResponse<AuthResponse>> CompleteActivationAsync(long orderCode);
    Task<ApiResponse<bool>> CancelActivationAsync(long orderCode);
}

public class PaymentApiService : IPaymentService
{
    private readonly HttpClient _httpClient;

    public PaymentApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponse<CoachActivationPreview>> PrepareCoachActivationAsync(RegisterRequest draft)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("payments/coach-activation/prepare", draft);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<CoachActivationPreview>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<CoachActivationPreview>
            {
                Success = false,
                Message = $"Could not prepare the order (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<CoachActivationPreview>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<ActivationLink>> CreateActivationLinkAsync(RegisterRequest draft)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("payments/coach-activation/create-link", draft);
            var (result, rawPreview) = await ApiResponseReader.ReadAsync<ActivationLink>(response);
            if (result != null)
            {
                return result;
            }

            return ApiResponseReader.Unexpected<ActivationLink>(
                response, rawPreview, $"Could not create the payment link (Code: {response.StatusCode})");
        }
        catch (Exception ex)
        {
            return new ApiResponse<ActivationLink>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<AuthResponse>> CompleteActivationAsync(long orderCode)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "payments/coach-activation/complete", new { orderCode });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Could not verify the payment (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<bool>> CancelActivationAsync(long orderCode)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "payments/coach-activation/cancel", new { orderCode });
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            if (result != null)
            {
                return result;
            }

            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Could not cancel the order (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = $"Connection error: {ex.Message}"
            };
        }
    }
}
