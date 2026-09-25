using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        var result = await _locationService.GetPagedLocationsAsync(effectiveSearch, pageNumber, pageSize, cancellationToken);
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
        var result = await _locationService.GetPublicLocationsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new location (Admin / Staff only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLocation(
        [FromBody] CreateLocationDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _locationService.CreateLocationAsync(dto, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(result);
            }

            return BadRequest(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Update an existing location (Admin / Staff only)
    /// </summary>
    [HttpPut("{locationId:guid}")]
    [Authorize(Roles = $"{RoleConstants.Staff},{RoleConstants.Admin}")]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponseDto<LocationDto>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLocation(
        [FromRoute] Guid locationId,
        [FromBody] UpdateLocationDto dto,
        CancellationToken cancellationToken = default)
    {
        var result = await _locationService.UpdateLocationAsync(locationId, dto, cancellationToken);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }

            if (result.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(result);
            }

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
