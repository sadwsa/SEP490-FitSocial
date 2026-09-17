using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
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

        [HttpGet("my-packages")]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> GetMyPackages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(new { message = "Invalid token or user ID" });
            }

            var result = await _service.GetMyPackagesAsync(currentUserId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPackageById(Guid id)
        {
            var result = await _service.GetPackageByIdAsync(id);
            if (result == null)
            {
                return NotFound(new { message = "Training package not found" });
            }
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> CreatePackage([FromBody] CreateTrainingPackageDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(new { message = "Invalid token or user ID" });
            }

            var result = await _service.CreatePackageAsync(currentUserId, dto);

            // Theo chuẩn RESTful, Create xong sẽ trả về 201 Created cùng URL để lấy chi tiết
            return CreatedAtAction(nameof(GetPackageById), new { id = result.PackageId }, result);
        }
    }
}
