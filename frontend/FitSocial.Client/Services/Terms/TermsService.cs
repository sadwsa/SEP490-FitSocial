using System.Net.Http.Json;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Terms;

public class TermsDto
{
    public Guid TermId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
}

public interface ITermsService
{
    Task<TermsDto?> GetLatestAsync();
}

public class TermsService : ITermsService
{
    private readonly HttpClient _http;

    public TermsService(HttpClient http)
    {
        _http = http;
    }

    public async Task<TermsDto?> GetLatestAsync()
    {
        try
        {
            var resp = await _http.GetAsync("terms/latest");
            if (!resp.IsSuccessStatusCode) return null;
            var wrap = await resp.Content.ReadFromJsonAsync<ApiResponse<TermsDto>>();
            return wrap?.Data;
        }
        catch
        {
            return null;
        }
    }
}
