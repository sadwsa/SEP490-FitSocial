using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SubscriptionPriceHistory;

namespace FitSocial.Application.Interfaces;

/// <summary>
/// Service interface for retrieving Coach Subscription Plan price change histories.
/// </summary>
public interface ISubscriptionPriceHistoryService
{
    Task<ApiResponseDto<PagedResultDto<SubscriptionPriceHistoryDto>>> GetPriceHistoryAsync(
        GetPriceHistoryFilterDto filter,
        CancellationToken cancellationToken = default);
}
