using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SportsController : ControllerBase
{
    private readonly ISportService _sportService;
    private readonly ISportRepository _sportRepository;

    public SportsController(ISportService sportService, ISportRepository sportRepository)
    {
        _sportService = sportService;
        _sportRepository = sportRepository;
    }

    /// <summary>
    /// Get full sport details list (Admin/Staff management)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<List<SportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSports(CancellationToken cancellationToken)
    {
        var result = await _sportService.GetSportListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new sport (Admin/Staff only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<SportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSport([FromBody] CreateSportDto dto, CancellationToken cancellationToken)
    {
        var result = await _sportService.CreateSportAsync(dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Update an existing sport (Admin/Staff only)
    /// </summary>
    [HttpPut("{sportId:guid}")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<SportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSport([FromRoute] Guid sportId, [FromBody] UpdateSportDto dto, CancellationToken cancellationToken)
    {
        var result = await _sportService.UpdateSportAsync(sportId, dto, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Delete a sport (Admin/Staff only)
    /// </summary>
    [HttpDelete("{sportId:guid}")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSport([FromRoute] Guid sportId, CancellationToken cancellationToken)
    {
        var result = await _sportService.DeleteSportAsync(sportId, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Get simple sport list for public dropdowns / registration screen
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<SportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicSports(CancellationToken cancellationToken)
    {
        var sports = await _sportRepository.ListAllAsync(cancellationToken);

        var result = sports.Select(s => new SportDto
        {
            SportId = s.SportId,
            SportName = s.SportName
        }).ToList();

        return Ok(ApiResponseDto<List<SportDto>>.Ok(result, "Sports list retrieved successfully."));
    }
}
