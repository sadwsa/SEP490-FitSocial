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
    /// Gets the profile of the currently authenticated Staff or Admin user.
    /// Only accessible by STAFF and ADMIN roles.
    /// Returns 7 core profile fields without internal security details.
    /// </summary>
    [HttpGet("me/profile")]
    [HttpGet("~/api/admin/users/me/profile")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<StaffAdminOwnProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
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
            return Unauthorized(ApiResponseDto<StaffAdminOwnProfileDto>.Fail("Invalid user identity claim."));
        }

        var result = await _userService.GetStaffAdminProfileAsync(userId, HttpContext.RequestAborted);
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
