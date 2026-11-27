using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.PaymentGateway;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleConstants.Admin)]
public class PaymentGatewayConfigsController : ControllerBase
{
    private readonly IPaymentGatewayConfigService _configService;

    public PaymentGatewayConfigsController(IPaymentGatewayConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<PaymentGatewayConfigDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllConfigs(CancellationToken cancellationToken = default)
    {
        var result = await _configService.GetAllConfigsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentGatewayConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _configService.GetConfigByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentGatewayConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateConfig([FromBody] CreatePaymentGatewayConfigDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized(ApiResponseDto<PaymentGatewayConfigDto>.Fail("Unauthorized"));
        }

        var result = await _configService.CreateConfigAsync(adminUserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentGatewayConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateConfig(Guid id, [FromBody] UpdatePaymentGatewayConfigDto request, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var adminUserId))
        {
            return Unauthorized(ApiResponseDto<PaymentGatewayConfigDto>.Fail("Unauthorized"));
        }

        var result = await _configService.UpdateConfigAsync(id, adminUserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteConfig(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _configService.DeleteConfigAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateConfig(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _configService.ActivateConfigAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateConfig(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _configService.DeactivateConfigAsync(id, cancellationToken);
        return Ok(result);
    }
}
