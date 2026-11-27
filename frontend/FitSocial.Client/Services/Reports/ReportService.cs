using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Reports;
using FitSocial.Client.Services.Auth;
using FitSocial.Client.Services.Http;
using Microsoft.AspNetCore.Components.Forms;

namespace FitSocial.Client.Services.Reports;

public class ReportService : IReportService
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private const string BaseEndpoint = "reports";

    public ReportService(ApiClient apiClient, HttpClient httpClient, ITokenStorage tokenStorage)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
    }

    public async Task<ApiResponse<PagedResult<ReportListItemDto>>> GetReportsAsync(
        string? searchTerm = null,
        string? status = null,
        string? targetType = null,
        Guid? reporterId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            var query = new List<string>();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query.Add($"status={Uri.EscapeDataString(status.Trim().ToUpperInvariant())}");
            }

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                query.Add($"targetType={Uri.EscapeDataString(targetType.Trim().ToUpperInvariant())}");
            }

            if (reporterId.HasValue && reporterId.Value != Guid.Empty)
            {
                query.Add($"reporterId={reporterId.Value}");
            }

            if (fromDate.HasValue)
            {
                query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("o"))}");
            }

            if (toDate.HasValue)
            {
                query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("o"))}");
            }

            query.Add($"pageNumber={Math.Max(1, pageNumber)}");
            query.Add($"pageSize={Math.Max(1, pageSize)}");

            var endpoint = "reports";
            if (query.Count > 0)
            {
                endpoint += "?" + string.Join("&", query);
            }

            var result = await _apiClient.GetAsync<PagedResult<ReportListItemDto>>(endpoint);
            if (result != null && result.Success && result.Data == null)
            {
                result.Data = new PagedResult<ReportListItemDto>();
            }

            return result ?? new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = "Unable to load reports. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = $"Error loading reports: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<ReportListItemDto>> GetReportByIdAsync(Guid reportId)
    {
        try
        {
            var result = await _apiClient.GetAsync<ReportListItemDto>($"reports/{reportId}");
            return result ?? new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = "Unable to load report details."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = $"Error loading report details: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<ReportListItemDto>> ProcessReportAsync(Guid reportId, ProcessViolationReportRequest request)
    {
        try
        {
            var result = await _apiClient.PostAsync<ProcessViolationReportRequest, ReportListItemDto>($"reports/{reportId}/process", request);
            return result ?? new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = "Unable to process report."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = $"Error processing report: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<PagedResult<ReportListItemDto>>> GetAppealsAsync(
        string? appealStatus = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        try
        {
            var query = new List<string>();

            // Luôn truyền appealStatus (nếu rỗng hoặc ALL thì truyền ALL để Backend chỉ lọc các bản ghi có kháng cáo)
            var effectiveStatus = string.IsNullOrWhiteSpace(appealStatus) ? "ALL" : appealStatus.Trim().ToUpperInvariant();
            query.Add($"appealStatus={Uri.EscapeDataString(effectiveStatus)}");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            query.Add($"pageNumber={Math.Max(1, pageNumber)}");
            query.Add($"pageSize={Math.Max(1, pageSize)}");

            var endpoint = "reports?" + string.Join("&", query);

            var result = await _apiClient.GetAsync<PagedResult<ReportListItemDto>>(endpoint);
            if (result != null && result.Success && result.Data == null)
            {
                result.Data = new PagedResult<ReportListItemDto>();
            }

            return result ?? new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = "Unable to load appeals. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PagedResult<ReportListItemDto>>
            {
                Success = false,
                Message = $"Error loading appeals: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<ReportListItemDto>> ReviewAppealAsync(Guid reportId, ReviewAppealRequest request)
    {
        try
        {
            var result = await _apiClient.PutAsync<ReviewAppealRequest, ReportListItemDto>($"reports/{reportId}/appeal", request);
            return result ?? new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = "Unable to review appeal."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = $"Error reviewing appeal: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<ReportListItemDto>> ReportUserAsync(CreateUserReportRequest request)
    {
        try
        {
            var result = await _apiClient.PostAsync<CreateUserReportRequest, ReportListItemDto>("reports/user", request);
            return result ?? new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = "Unable to submit user report."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportListItemDto>
            {
                Success = false,
                Message = $"Error reporting user: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<List<string>>> UploadReportImagesAsync(IBrowserFile[] files)
    {
        if (files == null || files.Length == 0)
        {
            return new ApiResponse<List<string>> { Success = false, Message = "No images selected." };
        }

        try
        {
            var buffered = new List<(byte[] Bytes, string FileName, string ContentType)>(files.Length);
            foreach (var file in files)
            {
                using var ms = new MemoryStream();
                await file.OpenReadStream(10 * 1024 * 1024).CopyToAsync(ms);
                var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType;
                buffered.Add((ms.ToArray(), file.Name, contentType));
            }

            return await _apiClient.PostMultipartAsync<List<string>>($"{BaseEndpoint}/upload-images", () =>
            {
                var content = new MultipartFormDataContent();
                foreach (var item in buffered)
                {
                    var byteContent = new ByteArrayContent(item.Bytes);
                    byteContent.Headers.ContentType = new MediaTypeHeaderValue(item.ContentType);
                    content.Add(byteContent, "files", item.FileName);
                }
                return content;
            });
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<string>>
            {
                Success = false,
                Message = $"Error uploading images: {ex.Message}"
            };
        }
    }
}


