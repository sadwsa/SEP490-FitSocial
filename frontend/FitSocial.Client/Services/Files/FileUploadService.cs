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
    Task<ApiResponse<List<CreatePostMediaItemDto>>> UploadPostMediaAsync(IEnumerable<UploadMediaPayload> files);
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileList = files.ToList();
        long totalBytes = fileList.Sum(f => f.Size);

        Console.WriteLine($"[FE][Upload] Starting upload for {fileList.Count} files. Total payload: {totalBytes / (1024.0 * 1024.0):F2} MB");
        for (int i = 0; i < fileList.Count; i++)
        {
            Console.WriteLine($"[FE][Upload] File #{i + 1}: '{fileList[i].Name}' ({fileList[i].Size / 1024.0:F0} KB, ContentType: {fileList[i].ContentType})");
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var token = await _tokenStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            foreach (var file in fileList)
            {
                var stream = file.OpenReadStream(MaxMediaBytes);
                content.Add(new StreamContent(stream), "files", file.Name);
            }

            var response = await _httpClient.PostAsync("upload/post-media", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CreatePostMediaItemDto>>>();

            sw.Stop();
            Console.WriteLine($"[FE][Upload] Completed in {sw.ElapsedMilliseconds} ms (Status: {response.StatusCode})");

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
            sw.Stop();
            Console.WriteLine($"[FE][Upload] Failed after {sw.ElapsedMilliseconds} ms: {ex.Message}");
            return new ApiResponse<List<CreatePostMediaItemDto>>
            {
                Success = false,
                Message = $"Media upload error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<List<CreatePostMediaItemDto>>> UploadPostMediaAsync(IEnumerable<UploadMediaPayload> files)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileList = files.ToList();
        long totalBytes = fileList.Sum(f => (long)f.Data.Length);

        Console.WriteLine($"[FE][Upload] Starting in-memory upload for {fileList.Count} files. Total payload: {totalBytes / (1024.0 * 1024.0):F2} MB");
        for (int i = 0; i < fileList.Count; i++)
        {
            Console.WriteLine($"[FE][Upload] File #{i + 1}: '{fileList[i].FileName}' ({fileList[i].Data.Length / 1024.0:F0} KB, ContentType: {fileList[i].ContentType})");
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var token = await _tokenStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            foreach (var file in fileList)
            {
                var byteContent = new ByteArrayContent(file.Data);
                if (!string.IsNullOrWhiteSpace(file.ContentType))
                {
                    byteContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                }
                content.Add(byteContent, "files", file.FileName);
            }

            var response = await _httpClient.PostAsync("upload/post-media", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CreatePostMediaItemDto>>>();

            sw.Stop();
            Console.WriteLine($"[FE][Upload] Completed in {sw.ElapsedMilliseconds} ms (Status: {response.StatusCode})");

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
            sw.Stop();
            Console.WriteLine($"[FE][Upload] Failed after {sw.ElapsedMilliseconds} ms: {ex.Message}");
            return new ApiResponse<List<CreatePostMediaItemDto>>
            {
                Success = false,
                Message = $"Media upload error: {ex.Message}"
            };
        }
    }
}
