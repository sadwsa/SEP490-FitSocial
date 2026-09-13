using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SportsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public SportsController(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get the list of sports (used by the registration screen for goal selection, multiple allowed)
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<SportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSports()
    {
        var sports = await _context.Sports
            .OrderBy(s => s.SportName)
            .Select(s => new SportDto
            {
                SportId = s.SportId,
                SportName = s.SportName
            })
            .ToListAsync();

        return Ok(ApiResponseDto<List<SportDto>>.Ok(sports, "Sports list retrieved successfully."));
    }
}
