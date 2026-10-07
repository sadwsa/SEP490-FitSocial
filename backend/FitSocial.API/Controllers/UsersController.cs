using System;
using System.Security.Claims;
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
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Gets the profile of the currently authenticated user.
    /// Staff/Admin users receive their 7 core profile fields.
    /// Trainee/Coach users receive their role-specific profile information.
    /// </summary>
    [HttpGet("me/profile")]
    [ProducesResponseType(typeof(ApiResponseDto<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<StaffAdminOwnProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value
                          ?? User.FindFirst("id")?.Value
                          ?? User.FindFirst("uid")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponseDto<object>.Fail("Invalid user identity claim."));
        }

        var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value?.Trim().ToUpperInvariant();
        if (RoleConstants.IsAdminOrStaff(roleClaim))
        {
            var staffResult = await _userService.GetStaffAdminProfileAsync(userId, HttpContext.RequestAborted);
            return Ok(staffResult);
        }

        var result = await _userService.GetOwnProfileAsync(userId, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Gets the profile of the currently authenticated Staff or Admin user.
    /// Only accessible by STAFF and ADMIN roles.
    /// </summary>
    [HttpGet("~/api/admin/users/me/profile")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<StaffAdminOwnProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaffAdminOwnProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value
                          ?? User.FindFirst("id")?.Value
                          ?? User.FindFirst("uid")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponseDto<StaffAdminOwnProfileDto>.Fail("Invalid user identity claim."));
        }

        var result = await _userService.GetStaffAdminProfileAsync(userId, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Updates the profile of the currently authenticated Staff or Admin user (UC_35.1).
    /// Only accessible by STAFF and ADMIN roles.
    /// Retains existing data for blank/empty fields; validates and updates new data.
    /// </summary>
    [HttpPut("~/api/admin/users/me/profile")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<StaffAdminOwnProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStaffAdminProfile([FromBody] UpdateStaffAdminProfileRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value
                          ?? User.FindFirst("id")?.Value
                          ?? User.FindFirst("uid")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponseDto<StaffAdminOwnProfileDto>.Fail("Invalid user identity claim."));
        }

        var result = await _userService.UpdateStaffAdminProfileAsync(userId, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Updates the profile of the currently authenticated Trainee or Coach user (UC_05.1).
    /// Uses authenticated claims to guarantee users can only update their own profile.
    /// </summary>
    [HttpPut("me/profile")]
    [Authorize(Roles = $"{RoleConstants.Trainee},{RoleConstants.Coach}")]
    [ProducesResponseType(typeof(ApiResponseDto<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateOwnProfile([FromBody] UpdateOwnProfileRequestDto request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value
                          ?? User.FindFirst("nameid")?.Value
                          ?? User.FindFirst("id")?.Value
                          ?? User.FindFirst("uid")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponseDto<object>.Fail("Invalid user identity claim."));
        }

        var result = await _userService.UpdateOwnProfileAsync(userId, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    /// <summary>
    /// Gets the profile of another user by userId (Trainee or Coach).
    /// Requires authentication. Does not expose sensitive credentials, certificates, or coach sports.
    /// </summary>
    [HttpGet("{userId:guid}/profile")]
    [ProducesResponseType(typeof(ApiResponseDto<UserProfileResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfile([FromRoute] Guid userId)
    {
        var result = await _userService.GetUserProfileAsync(userId, HttpContext.RequestAborted);
        return Ok(result);
    }
}
