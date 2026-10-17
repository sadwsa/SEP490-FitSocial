using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Finance;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Finance;

public class MoneyMovementService : IMoneyMovementService
{
    private readonly ApiClient _apiClient;

    public MoneyMovementService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<MoneyMovementListDto>> GetMoneyMovementsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? movementType = null,
        string? recipientRole = null,
        string? status = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        try
        {
            var query = new List<string>();

            if (fromDate.HasValue)
            {
                query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("yyyy-MM-dd"))}");
            }

            if (toDate.HasValue)
            {
                query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("yyyy-MM-dd"))}");
            }

            if (!string.IsNullOrWhiteSpace(movementType))
            {
                query.Add($"movementType={Uri.EscapeDataString(movementType.Trim().ToUpperInvariant())}");
            }

            if (!string.IsNullOrWhiteSpace(recipientRole))
            {
                query.Add($"recipientRole={Uri.EscapeDataString(recipientRole.Trim().ToUpperInvariant())}");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query.Add($"status={Uri.EscapeDataString(status.Trim().ToUpperInvariant())}");
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
            }

            query.Add($"pageNumber={Math.Max(1, pageNumber)}");
            query.Add($"pageSize={Math.Max(1, pageSize)}");

            var result = await _apiClient.GetAsync<MoneyMovementListDto>(
                "money-movements?" + string.Join("&", query));

            if (result != null && result.Success && result.Data == null)
            {
                result.Data = new MoneyMovementListDto();
            }

            return result ?? new ApiResponse<MoneyMovementListDto>
            {
                Success = false,
                Message = "Unable to load money movements."
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<MoneyMovementListDto>
            {
                Success = false,
                Message = $"Error loading money movements: {ex.Message}"
            };
        }
    }
}