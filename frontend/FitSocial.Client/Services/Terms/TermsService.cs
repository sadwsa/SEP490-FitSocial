using FitSocial.Client.Models.Terms;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Terms;

public interface ITermsService
{
    Task<TermsDto?> GetLatestAsync();
}

public class TermsService : ITermsService
{
    private readonly ApiClient _apiClient;

    public TermsService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<TermsDto?> GetLatestAsync()
    {
        var res = await _apiClient.GetAsync<TermsDto>("terms/latest");
        return res.Success ? res.Data : null;
    }
}