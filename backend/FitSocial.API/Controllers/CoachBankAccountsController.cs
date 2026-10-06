using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

/// <summary>
/// UC-36 Admin View List Payment Accounts (Staff, Admin).
/// </summary>
[ApiController]
[Route("api/coach-bank-accounts")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class CoachBankAccountsController : ControllerBase
{
    private readonly ICoachBankAccountService _bankAccountService;

    public CoachBankAccountsController(ICoachBankAccountService bankAccountService)
    {
        _bankAccountService = bankAccountService;
    }

    /// <summary>
    /// Lists all coach payment accounts with optional search / bank / default filters.
    /// Account numbers are masked.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<CoachPaymentAccountListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? bankCode,
        [FromQuery] bool? defaultOnly,
        CancellationToken cancellationToken = default)
    {
        var result = await _bankAccountService.GetAllPaymentAccountsAsync(search, bankCode, defaultOnly, cancellationToken);
        return Ok(result);
    }
}
