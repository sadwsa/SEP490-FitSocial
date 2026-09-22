using System.Net.Http.Json;

namespace FitSocial.Client.Services.Ekyc;

public class EkycRequest
{
    public string FrontCardUrl { get; set; } = string.Empty;
    public string BackCardUrl { get; set; } = string.Empty;
    public string? FaceImageUrl { get; set; }
    public string? FaceImageLeftUrl { get; set; }
    public string? FaceImageRightUrl { get; set; }
    public string? FaceImageTopUrl { get; set; }
    public string? FaceImageBottomUrl { get; set; }
}

public interface IEkycService
{
    Task<(bool Success, string? Error, EkycResult? Data)> VerifyAsync(EkycRequest request);
}

public class EkycResult
{
    public string IdCardNumber { get; set; } = string.Empty;
    public string FullNameOnCard { get; set; } = string.Empty;
    public DateOnly? DateOfBirthOnCard { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
}

public class EkycService : IEkycService
{
    private readonly HttpClient _http;

    public EkycService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, string? Error, EkycResult? Data)> VerifyAsync(EkycRequest request)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("ekyc/verify", request);
            var raw = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                // backend returns {success, data: EkycExtract}
                var doc = System.Text.Json.JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    var r = System.Text.Json.JsonSerializer.Deserialize<EkycResult>(dataEl.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return (true, null, r);
                }
                return (true, null, null);
            }
            else
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("message", out var msg))
                        return (false, msg.GetString(), null);
                }
                catch { }
                return (false, "ID verification failed.", null);
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }
}
