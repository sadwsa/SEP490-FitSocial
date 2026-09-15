using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SportsController : ControllerBase
{
    private readonly ISportRepository _sports;

    public SportsController(ISportRepository sports)
    {
        _sports = sports;
    }

    /// <summary>
    /// Get the list of sports (used by the registration screen for goal selection, multiple allowed)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<SportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSports()
    {
        var sports = await _sports.ListAllAsync();

        var result = sports.Select(s => new SportDto
        {
            SportId = s.SportId,
            SportName = s.SportName
        }).ToList();

        return Ok(ApiResponseDto<List<SportDto>>.Ok(result, "Sports list retrieved successfully."));
    }
}
