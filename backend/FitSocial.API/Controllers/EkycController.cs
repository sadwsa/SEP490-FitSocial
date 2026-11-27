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
