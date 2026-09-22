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
}
