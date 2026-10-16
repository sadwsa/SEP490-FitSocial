using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EkycController : ControllerBase
{
    private readonly IEkycService _ekycService;

    public EkycController(IEkycService ekycService)
    {
        _ekycService = ekycService;
    }

    /// <summary>
    /// Verify front/back ID card + liveness face via Viettel eKYC (or mock).
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    public async Task<IActionResult> Verify([FromBody] EkycVerificationDto request)
    {
        var result = await _ekycService.VerifyAsync(request);
        if (!result.Success)
        {
            return BadRequest(new { success = false, message = result.Error });
        }
        return Ok(new { success = true, data = result.Result });
    }

    public record CheckIdCardRequest(string IdCardNumber, string? Email);

    /// <summary>
    /// Check if CCCD has already been used by another coach. Called right after OCR on Step 2 so user sees duplicate immediately on CCCD page.
    /// </summary>
    [HttpPost("check-idcard")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckIdCard([FromBody] CheckIdCardRequest request)
    {
        var (available, error) = await _ekycService.CheckIdCardAsync(request.IdCardNumber, request.Email);
        if (!available)
            return BadRequest(new { success = false, message = error ?? "IdCardNumber is required." });

        return Ok(new { success = true });
    }
}
