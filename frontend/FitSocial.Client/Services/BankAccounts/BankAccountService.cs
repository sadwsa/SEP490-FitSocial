using FitSocial.Client.Models.BankAccounts;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.BankAccounts;

/// <summary>
/// UC-36: Staff/Admin view of all coach payment accounts.
/// </summary>
public interface IBankAccountService
{
    Task<ApiResponse<List<CoachPaymentAccount>>> GetAllAsync(string? search = null, string? bankCode = null, bool? defaultOnly = null);
}

public class BankAccountApiService : IBankAccountService
{
    private readonly ApiClient _apiClient;

    public BankAccountApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<ApiResponse<List<CoachPaymentAccount>>> GetAllAsync(string? search = null, string? bankCode = null, bool? defaultOnly = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(bankCode)) query.Add($"bankCode={Uri.EscapeDataString(bankCode.Trim())}");
        if (defaultOnly == true) query.Add("defaultOnly=true");
        var endpoint = "coach-bank-accounts" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return _apiClient.GetAsync<List<CoachPaymentAccount>>(endpoint);
    }
}
