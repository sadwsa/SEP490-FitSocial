using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Analytics;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Services;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// UC_27: Admin View System Revenue Analytics.
/// Provides platform financial metrics, GMV, direct platform revenue, refund tracking, and CSV export.
/// </summary>
[ApiController]
[Route("api/admin/analytics/revenue")]
[Authorize(Roles = RoleConstants.Admin)]
public class AdminRevenueAnalyticsController : ControllerBase
{
    private readonly IRevenueAnalyticsService _revenueAnalyticsService;

    public AdminRevenueAnalyticsController(IRevenueAnalyticsService revenueAnalyticsService)
    {
        _revenueAnalyticsService = revenueAnalyticsService;
    }

    /// <summary>
    /// Retrieves full revenue analytics overview, time-series, breakdown, and top contributors.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(ApiResponseDto<FullRevenueAnalyticsResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueOverview(
        [FromQuery] RevenueAnalyticsFilterDto filter,
        CancellationToken ct = default)
    {
        var result = await _revenueAnalyticsService.GetRevenueAnalyticsAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Exports revenue report as UTF-8 CSV with BOM for spreadsheet software (Excel, Sheets).
    /// </summary>
    [HttpGet("export-csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportRevenueCsv(
        [FromQuery] RevenueAnalyticsFilterDto filter,
        CancellationToken ct = default)
    {
        var csvBytes = await _revenueAnalyticsService.ExportRevenueCsvAsync(filter, ct);
        var fileName = $"FitSocial_Revenue_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }
}
