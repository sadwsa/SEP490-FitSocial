using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Get a paginated list of reports for Staff/Admin with optional filtering by status, target type, reporter, and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<ReportListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports([FromQuery] GetReportsQueryDto query, CancellationToken cancellationToken = default)
    {
        var result = await _reportService.GetReportsAsync(query, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
