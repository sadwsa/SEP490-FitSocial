using FitSocial.Client.Models.Ekyc;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Ekyc;

public interface IEkycService
{
    Task<(bool Success, string? Error, EkycResult? Data)> VerifyAsync(EkycRequest request);
    Task<(bool Success, string? Error)> CheckIdCardAsync(string idCardNumber, string? email);
}

public class EkycService : IEkycService
{
    private readonly ApiClient _apiClient;

    public EkycService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<(bool Success, string? Error, EkycResult? Data)> VerifyAsync(EkycRequest request)
    {
        var res = await _apiClient.PostAsync<EkycRequest, EkycResult>("ekyc/verify", request);
        if (res.Success)
        {
            return (true, null, res.Data);
        }
        return (false, res.Message ?? "ID verification failed.", null);
    }

    public async Task<(bool Success, string? Error)> CheckIdCardAsync(string idCardNumber, string? email)
    {
        var res = await _apiClient.PostAsync<object, object>("ekyc/check-idcard", new { IdCardNumber = idCardNumber, Email = email });
        if (res.Success)
        {
            return (true, null);
        }
        return (false, res.Message ?? "This ID card has already been used for another coach.");
    }
}
