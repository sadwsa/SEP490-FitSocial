using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrainingPackagesController : ControllerBase
    {
        private readonly ITrainingPackageService _service;

        public TrainingPackagesController(ITrainingPackageService service)
        {
            _service = service;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllPackages(
            [FromQuery] string? searchKeyword,
            [FromQuery] decimal? maxPrice,
            [FromQuery] Guid? coachId)
        {
            var result = await _service.GetAllPackagesAsync(searchKeyword, maxPrice, coachId);
            return Ok(ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>.Ok(result));
        }

        [HttpGet("my-packages")]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> GetMyPackages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>.Fail("Invalid token or user ID"));
            }

            var result = await _service.GetMyPackagesAsync(currentUserId);
            return Ok(ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>.Ok(result));
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackageById(Guid id)
        {
            var result = await _service.GetPackageByIdAsync(id);
            if (result == null)
            {
                return NotFound(ApiResponseDto<TrainingPackageResponseDto>.Fail("Training package not found"));
            }
            return Ok(ApiResponseDto<TrainingPackageResponseDto>.Ok(result));
        }

        [HttpPost]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> CreatePackage([FromBody] CreateTrainingPackageDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<TrainingPackageResponseDto>.Fail("Invalid token or user ID"));
            }

            var result = await _service.CreatePackageAsync(currentUserId, dto);

            return CreatedAtAction(nameof(GetPackageById), new { id = result.PackageId }, ApiResponseDto<TrainingPackageResponseDto>.Ok(result, "Package created successfully"));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdateTrainingPackageDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<TrainingPackageResponseDto>.Fail("Invalid token or user ID"));
            }

            var result = await _service.UpdatePackageAsync(id, currentUserId, dto);
            if (result == null)
            {
                return NotFound(ApiResponseDto<TrainingPackageResponseDto>.Fail("Training package not found or unauthorized"));
            }

            return Ok(ApiResponseDto<TrainingPackageResponseDto>.Ok(result, "Package updated successfully"));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> DeletePackage(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Invalid token or user ID"));
            }

            var success = await _service.SoftDeletePackageAsync(id, currentUserId);
            if (!success)
            {
                return BadRequest(ApiResponseDto<bool>.Fail("Cannot delete package or package not found/unauthorized."));
            }

            return Ok(ApiResponseDto<bool>.Ok(true, "Package soft deleted successfully"));
        }
    }
}
