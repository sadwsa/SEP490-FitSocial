using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Send the OTP verification code via Email (UC_01)
    /// </summary>
    [HttpPost("send-otp")]
    [EnableRateLimiting("OtpPolicy")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<bool>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.SendOtpAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Register a Trainee account with OTP verification (UC_01)
    /// </summary>
    [HttpPost("register-trainee")]
    [EnableRateLimiting("OtpPolicy")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterTrainee([FromBody] RegisterTraineeRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.RegisterTraineeAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Sign in with email + password, shared by all roles (UC_02)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.LoginAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Back-office sign-in for Staff/Admin only (UC_28 Management Portal)
    /// </summary>
    [HttpPost("admin-login")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.AdminLoginAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Server-side sign-out (UC_03): revokes the calling access token
    /// and, when provided, the refresh token.
    /// The client still clears its own storage afterwards.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request = null)
    {
        var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;

        DateTime? expiresAtUtc = null;
        var expValue = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(expValue, out var expSeconds))
        {
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
        }

        var result = await _authService.LogoutAsync(jti, expiresAtUtc, request?.RefreshToken);
        return Ok(result);
    }

    /// <summary>
    /// Rotates a valid refresh token into a brand-new session (new access + refresh tokens).
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.RefreshAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Sign in / Sign up with a Google authorization code (redirect flow, works without FedCM)
    /// </summary>
    [HttpPost("google/code")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCodeLogin([FromBody] GoogleCodeRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.GoogleCodeLoginAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
