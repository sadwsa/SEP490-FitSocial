using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.CoachSubscriptionPlans;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoachSubscriptionPlansController : ControllerBase
{
    private readonly ICoachSubscriptionPlanService _planService;

    public CoachSubscriptionPlansController(ICoachSubscriptionPlanService planService)
    {
        _planService = planService;
    }

    /// <summary>
    /// Gets all active subscription plans available for coaches to view and purchase.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<CoachSubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePlans(CancellationToken cancellationToken = default)
    {
        var result = await _planService.GetActivePlansAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets all subscription plans (Admin/Staff only).
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = $"{RoleConstants.Admin},{RoleConstants.Staff}")]
    [ProducesResponseType(typeof(ApiResponseDto<List<CoachSubscriptionPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPlans(CancellationToken cancellationToken = default)
    {
        var result = await _planService.GetAllPlansAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single subscription plan by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<CoachSubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _planService.GetPlanByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new coach subscription plan (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<CoachSubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan([FromBody] CreateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        var result = await _planService.CreatePlanAsync(dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing coach subscription plan (Admin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<CoachSubscriptionPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdateCoachSubscriptionPlanDto dto, CancellationToken cancellationToken = default)
    {
        var result = await _planService.UpdatePlanAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deactivates a coach subscription plan (Admin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlan(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _planService.DeletePlanAsync(id, cancellationToken);
        return Ok(result);
    }
}
