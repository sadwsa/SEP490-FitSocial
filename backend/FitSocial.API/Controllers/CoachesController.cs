using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoachesController : ControllerBase
{
    private readonly ICoachService _coachService;

    public CoachesController(ICoachService coachService)
    {
        _coachService = coachService;
    }

    /// <summary>
    /// Gets the top 3 (or specified count) coaches with the highest average rating in the system.
    /// Filtered to active/approved coaches only, sorted by average rating in descending order.
    /// Accessible publicly without authentication.
    /// </summary>
    [HttpGet("top")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<TopCoachDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopCoaches([FromQuery] int count = 3)
    {
        var result = await _coachService.GetTopCoachesAsync(count, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllCoaches([FromQuery] string? searchKeyword, [FromQuery] int? minExperience, [FromQuery] string? sortBy)
    {
        var result = await _coachService.GetAllCoachesAsync(searchKeyword, minExperience, sortBy);
        return Ok(result);
    }
}
