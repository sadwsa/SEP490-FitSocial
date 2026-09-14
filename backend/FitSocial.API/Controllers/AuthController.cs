using FitSocial.Application.DTOs.Auth;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
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
    /// Register a Coach account with OTP verification (creates a pending approval coach profile)
    /// </summary>
    [HttpPost("register-coach")]
    [EnableRateLimiting("OtpPolicy")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCoach([FromBody] RegisterCoachRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.RegisterCoachAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Sign in / Sign up with a Google account (validates the Google-issued ID Token)
    /// </summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<AuthResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Invalid data"));
        }

        var result = await _authService.GoogleLoginAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
