using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Finance;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// Read-only history of every amount the platform transfers out to a coach or a trainee.
/// Staff and Admin only. No state-changing actions live here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Route("api/money-movements")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class MoneyMovementsController : ControllerBase
{
    private readonly IMoneyMovementService _service;

    public MoneyMovementsController(IMoneyMovementService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<MoneyMovementListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] MoneyMovementQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetMoneyMovementsAsync(query, cancellationToken);
        return Ok(result);
    }
}