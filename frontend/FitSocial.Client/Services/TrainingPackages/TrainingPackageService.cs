using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPackages;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.TrainingPackages;

public class TrainingPackageService : ITrainingPackageService
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private const string BaseEndpoint = "trainingpackages";

    public TrainingPackageService(ApiClient apiClient, HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetMyPackagesAsync()
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}/my-packages");
    }

    public async Task<ApiResponse<TrainingPackageResponseDto>> GetPackageByIdAsync(Guid id)
    {
        return await _apiClient.GetAsync<TrainingPackageResponseDto>($"{BaseEndpoint}/{id}");
    }

    public async Task<ApiResponse<TrainingPackageResponseDto>> CreatePackageAsync(CreateTrainingPackageDto dto)
    {
        return await _apiClient.PostAsync<CreateTrainingPackageDto, TrainingPackageResponseDto>(BaseEndpoint, dto);
    }

    public async Task<ApiResponse<TrainingPackageResponseDto>> UpdatePackageAsync(Guid id, UpdateTrainingPackageDto dto)
    {
        return await _apiClient.PutAsync<UpdateTrainingPackageDto, TrainingPackageResponseDto>($"{BaseEndpoint}/{id}", dto);
    }

    public async Task<ApiResponse<bool>> DeletePackageAsync(Guid id)
    {
        return await _apiClient.DeleteAsync<bool>($"{BaseEndpoint}/{id}");
    }

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetPackagesByCoachIdAsync(Guid coachId)
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}?coachId={coachId}");
    }

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchKeyword)) queryParams.Add($"searchKeyword={Uri.EscapeDataString(searchKeyword)}");
        if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
        if (coachId.HasValue) queryParams.Add($"coachId={coachId.Value}");
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}{query}");
    }

    public async Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetPurchasedPackagesAsync()
    {
        return await _apiClient.GetAsync<IEnumerable<TrainingPackageResponseDto>>($"{BaseEndpoint}/purchased");
    }

    public async Task<ApiResponse<List<string>>> UploadPackageImagesAsync(IEnumerable<IBrowserFile> files)
    {
        try
        {
            var fileList = files.ToList();
            if (fileList.Count == 0)
            {
                return new ApiResponse<List<string>> { Success = false, Message = "Vui lòng chọn ít nhất 1 ảnh." };
            }

            using var content = new MultipartFormDataContent();
            var token = await _tokenStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            foreach (var file in fileList)
            {
                var stream = file.OpenReadStream(10 * 1024 * 1024); // max 10MB/file
                content.Add(new StreamContent(stream), "files", file.Name);
            }

            var response = await _httpClient.PostAsync("upload/package-media", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
            if (response.IsSuccessStatusCode && result != null && result.Success)
            {
                return result;
            }

            return result ?? new ApiResponse<List<string>>
            {
                Success = false,
                Message = $"Upload ảnh thất bại (Status code: {response.StatusCode})"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<string>>
            {
                Success = false,
                Message = $"Lỗi khi tải ảnh: {ex.Message}"
            };
        }
    }
    // ==========================================
    // REVIEWS & REPLY CALLS
    // ==========================================
    public async Task<ApiResponse<PackageReviewSummaryDto>> GetPackageReviewsAsync(Guid packageId)
    {
        return await _apiClient.GetAsync<PackageReviewSummaryDto>($"{BaseEndpoint}/{packageId}/reviews");
    }

    public async Task<ApiResponse<bool>> SubmitPackageReviewAsync(Guid packageId, CreatePackageReviewDto dto)
    {
        return await _apiClient.PostAsync<CreatePackageReviewDto, bool>($"{BaseEndpoint}/{packageId}/reviews", dto);
    }

    public async Task<ApiResponse<bool>> ReplyToReviewAsync(Guid packageId, Guid reviewId, ReplyReviewDto dto)
    {
        return await _apiClient.PostAsync<ReplyReviewDto, bool>($"{BaseEndpoint}/{packageId}/reviews/{reviewId}/reply", dto);
    }

}
