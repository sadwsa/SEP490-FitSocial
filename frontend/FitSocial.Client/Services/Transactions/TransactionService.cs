using System.Text;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Transactions;
using FitSocial.Client.Services.Http;

namespace FitSocial.Client.Services.Transactions;

public class TransactionService : ITransactionService
{
    private readonly ApiClient _apiClient;

    public TransactionService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<PagedResult<TransactionHistoryDto>>> GetMyTransactionsAsync(TransactionFilterDto filter)
    {
        var sb = new StringBuilder("transactions/my-history?");
        sb.Append($"PageIndex={filter.PageIndex}&PageSize={filter.PageSize}");

        if (filter.FromDate.HasValue)
            sb.Append($"&FromDate={filter.FromDate.Value:yyyy-MM-dd}");

        if (filter.ToDate.HasValue)
            sb.Append($"&ToDate={filter.ToDate.Value:yyyy-MM-dd}");

        if (!string.IsNullOrWhiteSpace(filter.Type))
            sb.Append($"&Type={Uri.EscapeDataString(filter.Type.Trim())}");

        var response = await _apiClient.GetAsync<PagedResult<TransactionHistoryDto>>(sb.ToString());
        return response ?? new ApiResponse<PagedResult<TransactionHistoryDto>>
        {
            Success = false,
            Message = "Unable to load transactions."
        };
    }
}
