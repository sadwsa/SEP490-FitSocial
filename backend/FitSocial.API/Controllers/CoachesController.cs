using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
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

    [HttpGet]
    public async Task<IActionResult> GetAllCoaches([FromQuery] string? searchKeyword, [FromQuery] int? minExperience, [FromQuery] string? sortBy)
    {
        var result = await _coachService.GetAllCoachesAsync(searchKeyword, minExperience, sortBy);
        return Ok(result);
    }
}
