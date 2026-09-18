using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationRepository _locations;

    public LocationsController(ILocationRepository locations)
    {
        _locations = locations;
    }

    /// <summary>
    /// Get the list of locations (used for selection when creating posts or filtering).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<LocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations()
    {
        var locations = await _locations.ListAllAsync(HttpContext.RequestAborted);

        var result = locations.Select(l => new LocationDto
        {
            LocationId = l.LocationId,
            LocationName = l.LocationName,
            Address = l.Address
        }).ToList();

        return Ok(ApiResponseDto<List<LocationDto>>.Ok(result, "Locations list retrieved successfully."));
    }
}
