using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SubscriptionPriceHistory;

namespace FitSocial.Client.Services.SubscriptionPriceHistory;

public interface ISubscriptionPriceHistoryApiService
{
    Task<ApiResponse<PagedResult<SubscriptionPriceHistoryDto>>> GetPriceHistoryAsync(GetPriceHistoryFilterRequest request);
}
