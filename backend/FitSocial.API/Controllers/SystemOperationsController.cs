using FitSocial.Application.DTOs.SystemOperations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/system-operations")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class SystemOperationsController : ControllerBase
{
    private readonly ISystemOperationsService _systemOps;

    public SystemOperationsController(ISystemOperationsService systemOps)
    {
        _systemOps = systemOps;
    }

    /// <summary>
    /// UC_37: Staff/Admin View Coach Subscription Plans
    /// Query params: isActive, search, page, pageSize
    /// Returns Price plans with subscriber counts + recent CoachUpgrades
    /// </summary>
    [HttpGet("coach-subscription-plans")]
    public async Task<IActionResult> GetCoachSubscriptionPlans(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _systemOps.GetCoachSubscriptionPlansAsync(isActive, search, page, pageSize, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("coach-subscription-plans/{priceId:guid}")]
    public async Task<IActionResult> GetPlanById(Guid priceId, CancellationToken cancellationToken)
    {
        var result = await _systemOps.GetPlanByIdAsync(priceId, cancellationToken);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    /// <summary>
    /// UC_37.1: Staff/Admin Create Coach Subscription Plan
    /// </summary>
    [HttpPost("coach-subscription-plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(FitSocial.Application.DTOs.Common.ApiResponseDto<CoachSubscriptionPlanDto>.Fail(firstError ?? "Invalid data"));
        }
        var result = await _systemOps.CreatePlanAsync(dto, cancellationToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
