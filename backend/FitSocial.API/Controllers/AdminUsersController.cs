using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Users;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    /// <summary>
    /// Get user accounts for admin console with optional search and role filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<List<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] string? role, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.GetUsersAsync(search, role, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lock or unlock a user account (Admin / Staff only)
    /// </summary>
    [HttpPut("{userId:guid}/lock")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetUserLockStatus([FromRoute] Guid userId, [FromBody] UpdateUserLockStatusRequest request, CancellationToken cancellationToken)
    {
        Guid? adminId = null;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(subClaim, out var parsedAdminId))
        {
            adminId = parsedAdminId;
        }

        var result = await _adminUserService.SetUserLockStatusAsync(userId, request.IsLocked, adminId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
