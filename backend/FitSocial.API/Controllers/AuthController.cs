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
    /// Gửi mã xác thực OTP qua Email (UC_01)
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
            return BadRequest(ApiResponseDto<bool>.Fail(firstError ?? "Dữ liệu không hợp lệ"));
        }

        var result = await _authService.SendOtpAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Đăng ký tài khoản Học viên (Trainee) với xác thực mã OTP (UC_01)
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
            return BadRequest(ApiResponseDto<AuthResponseDto>.Fail(firstError ?? "Dữ liệu không hợp lệ"));
        }

        var result = await _authService.RegisterTraineeAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
