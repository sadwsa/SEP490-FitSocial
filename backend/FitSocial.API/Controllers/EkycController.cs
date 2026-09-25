using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EkycController : ControllerBase
{
    private readonly IEkycService _ekycService;
    private readonly ICoachEkycVerificationRepository _ekycRepo;
    private readonly IUserRepository _users;

    public EkycController(IEkycService ekycService, ICoachEkycVerificationRepository ekycRepo, IUserRepository users)
    {
        _ekycService = ekycService;
        _ekycRepo = ekycRepo;
        _users = users;
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
        if (string.IsNullOrWhiteSpace(request.IdCardNumber))
            return BadRequest(new { success = false, message = "IdCardNumber is required." });

        var normalizedId = request.IdCardNumber.Trim();
        Guid? excludeCoachId = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUser = await _users.FindByEmailAsync(request.Email.Trim().ToLower());
            if (existingUser != null) excludeCoachId = existingUser.UserId;
        }

        var exists = await _ekycRepo.ExistsByIdCardNumberAsync(normalizedId, excludeCoachId);
        if (exists)
            return BadRequest(new { success = false, message = "This ID card has already been used for another coach." });

        return Ok(new { success = true });
    }
}
