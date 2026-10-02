using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SubscriptionPriceHistory;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/admin/subscription-plans/price-history")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class SubscriptionPriceHistoryController : ControllerBase
{
    private readonly ISubscriptionPriceHistoryService _priceHistoryService;

    public SubscriptionPriceHistoryController(ISubscriptionPriceHistoryService priceHistoryService)
    {
        _priceHistoryService = priceHistoryService;
    }

    /// <summary>
    /// Gets paginated subscription price change history with optional filters (Admin and Staff only).
    /// </summary>
    /// <param name="filter">Paging and filtering options (PlanId, Keyword, FromDate, ToDate, PageIndex, PageSize)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paged list of subscription price history records</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<SubscriptionPriceHistoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPriceHistory(
        [FromQuery] GetPriceHistoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var result = await _priceHistoryService.GetPriceHistoryAsync(filter, cancellationToken);
        return Ok(result);
    }
}
