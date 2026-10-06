using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Get user accounts for admin console with optional search, role, and lock status filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isLocked,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.GetUsersAsync(search, role, isLocked, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get staff accounts for admin console with optional search, lock status filter, and pagination (Admin only - UC_34)
    /// </summary>
    [HttpGet("staff")]
    [HttpGet("~/api/admin/staff")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaff(
        [FromQuery] string? search,
        [FromQuery] bool? isLocked,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.GetStaffUsersAsync(search, isLocked, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new staff account (Admin only - UC_34.1)
    /// </summary>
    [HttpPost("staff")]
    [HttpPost("~/api/admin/staff")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AdminUserDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffAccountRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponseDto<AdminUserDto>.Fail(string.Join("; ", errors)));
        }

        var result = await _adminUserService.CreateStaffAccountAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Update an existing staff account (Admin only - UC_34.2)
    /// </summary>
    [HttpPut("staff/{userId:guid}")]
    [HttpPut("~/api/admin/staff/{userId:guid}")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<AdminUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AdminUserDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateStaff([FromRoute] Guid userId, [FromBody] UpdateStaffAccountRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponseDto<AdminUserDto>.Fail(string.Join("; ", errors)));
        }

        var result = await _adminUserService.UpdateStaffAccountAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Check whether an email is already registered in the system (Admin only)
    /// </summary>
    [HttpGet("check-email")]
    [HttpGet("~/api/admin/staff/check-email")]
    [Authorize(Roles = RoleConstants.Admin)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckEmail([FromQuery] string email, [FromQuery] Guid? excludingUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(ApiResponseDto<bool>.Fail("Email query parameter is required."));
        }

        var result = await _adminUserService.CheckEmailExistsAsync(email, excludingUserId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
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

    /// <summary>
    /// Issue a warning to a user account. If user reaches 3 warnings, account is auto-locked and token invalidated.
    /// </summary>
    [HttpPost("{userId:guid}/warn")]
    [ProducesResponseType(typeof(ApiResponseDto<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WarnUser([FromRoute] Guid userId, [FromBody] WarnUserRequestDto? request, CancellationToken cancellationToken)
    {
        Guid? staffId = null;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(subClaim, out var parsedStaffId))
        {
            staffId = parsedStaffId;
        }

        var result = await _adminUserService.WarnUserAsync(userId, request?.Reason, staffId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
