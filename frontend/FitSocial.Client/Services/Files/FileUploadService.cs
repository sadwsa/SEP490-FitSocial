using System.Net.Http.Json;
using FitSocial.Client.Models.Common;
using Microsoft.AspNetCore.Components.Forms;

namespace FitSocial.Client.Services.Files;

public interface IFileUploadService
{
    Task<ApiResponse<string>> UploadAsync(IBrowserFile file);
}

public class FileUploadService : IFileUploadService
{
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10MB
    private readonly HttpClient _httpClient;

    public FileUploadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResponse<string>> UploadAsync(IBrowserFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            await using var stream = file.OpenReadStream(MaxFileBytes);
            content.Add(new StreamContent(stream), "file", file.Name);

            var response = await _httpClient.PostAsync("upload", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<string>
            {
                Success = false,
                Message = $"Upload failed (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<string>
            {
                Success = false,
                Message = $"Upload failed: {ex.Message}"
            };
        }
    }
}
