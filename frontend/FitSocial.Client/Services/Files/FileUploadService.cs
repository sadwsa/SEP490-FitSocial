using System.Net.Http.Headers;
using System.Net.Http.Json;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Posts;
using FitSocial.Client.Services.Auth;
using Microsoft.AspNetCore.Components.Forms;

namespace FitSocial.Client.Services.Files;

public interface IFileUploadService
{
    Task<ApiResponse<string>> UploadAsync(IBrowserFile file);
    Task<ApiResponse<List<CreatePostMediaItemDto>>> UploadPostMediaAsync(IEnumerable<IBrowserFile> files);
}

public class FileUploadService : IFileUploadService
{
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10MB
    private const long MaxMediaBytes = 50 * 1024 * 1024; // 50MB for post videos/images
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;

    public FileUploadService(HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
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

    public async Task<ApiResponse<List<CreatePostMediaItemDto>>> UploadPostMediaAsync(IEnumerable<IBrowserFile> files)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var token = await _tokenStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            foreach (var file in files)
            {
                var stream = file.OpenReadStream(MaxMediaBytes);
                content.Add(new StreamContent(stream), "files", file.Name);
            }

            var response = await _httpClient.PostAsync("upload/post-media", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CreatePostMediaItemDto>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<List<CreatePostMediaItemDto>>
            {
                Success = false,
                Message = $"Media upload failed (Code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<CreatePostMediaItemDto>>
            {
                Success = false,
                Message = $"Media upload error: {ex.Message}"
            };
        }
    }
}
