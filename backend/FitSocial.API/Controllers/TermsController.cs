using FitSocial.Application.Interfaces;
using FitSocial.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TermsController : ControllerBase
{
    private readonly ITermsService _termsService;

    public TermsController(ITermsService termsService)
    {
        _termsService = termsService;
    }

    /// <summary>
    /// Get the latest effective terms and policies (for registration agreement).
    /// </summary>
    [HttpGet("latest")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<TermsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatest()
    {
        var term = await _termsService.GetLatestAsync();
        if (term == null)
        {
            return NotFound(ApiResponseDto<TermsDto>.Fail("No terms found."));
        }
        return Ok(ApiResponseDto<TermsDto>.Ok(term));
    }
}
