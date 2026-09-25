using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Domain.Constants;
using FitSocial.Domain.Interfaces;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    /// <summary>
    /// Get paginated and searchable location list for Admin and Staff console.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResultDto<LocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocations(
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var effectiveSearch = !string.IsNullOrWhiteSpace(searchTerm) ? searchTerm : search;
        var (items, totalCount) = await _locations.ListLocationsAsync(effectiveSearch, pageNumber, pageSize, cancellationToken);

        var dtos = items.Select(l => new LocationDto
        {
            LocationId = l.LocationId,
            LocationName = l.LocationName,
            Address = l.Address
        }).ToList();

        var pagedResult = PagedResultDto<LocationDto>.Create(dtos, totalCount, pageNumber, pageSize);
        return Ok(ApiResponseDto<PagedResultDto<LocationDto>>.Ok(pagedResult, "Locations list retrieved successfully."));
        var result = await _locationService.GetLocationsAsync(effectiveSearch, pageNumber, pageSize, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete an existing location (Admin / Staff only)
    /// </summary>
    [HttpDelete("{locationId:guid}")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteLocation([FromRoute] Guid locationId, CancellationToken cancellationToken = default)
    {
        var result = await _locationService.DeleteLocationAsync(locationId, cancellationToken);
        if (!result.Success)
        {
            if (result.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(result);
            }

            if (result.Message?.Contains("used by", StringComparison.OrdinalIgnoreCase) == true ||
                result.Message?.Contains("in use", StringComparison.OrdinalIgnoreCase) == true ||
                result.Message?.Contains("linked", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Conflict(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get full location list for public selection dropdowns (e.g. creating posts, coach filter).
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<LocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicLocations(CancellationToken cancellationToken = default)
    {
        var locations = await _locations.ListAllAsync(cancellationToken);

        var result = locations.Select(l => new LocationDto
        var result = await _locationService.GetPublicLocationsAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

