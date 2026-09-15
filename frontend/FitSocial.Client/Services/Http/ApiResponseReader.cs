using System.Text.Json;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Http;

/// <summary>
/// Reads the raw body first so a non-JSON server reply (proxy page, crash page, ...)
/// surfaces its real content instead of a cryptic "'X' is an invalid start" error.
/// </summary>
public static class ApiResponseReader
{
    public static async Task<(ApiResponse<T>? Result, string? RawPreview)> ReadAsync<T>(HttpResponseMessage response)
    {
        string raw;
        try
        {
            raw = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var preview = raw.Length > 300 ? raw[..300] : raw;
        try
        {
            var parsed = JsonSerializer.Deserialize<ApiResponse<T>>(
                raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return (parsed, preview);
        }
        catch
        {
            return (null, preview);
        }
    }

    public static ApiResponse<T> Unexpected<T>(HttpResponseMessage response, string? rawPreview, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(rawPreview))
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = $"Unexpected server response (HTTP {(int)response.StatusCode}): {rawPreview}"
            };
        }

        return new ApiResponse<T> { Success = false, Message = fallback };
    }
}
