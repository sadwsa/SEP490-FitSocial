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
        [HttpGet("purchased")]
        [Authorize] // Bất kỳ user nào đăng nhập đều có thể lấy danh sách họ đã mua
        public async Task<IActionResult> GetPurchasedPackages()
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>.Fail("Invalid token or user ID"));
            }

            var packages = await _service.GetPurchasedPackagesAsync(currentUserId);
            return Ok(ApiResponseDto<IEnumerable<TrainingPackageResponseDto>>.Ok(packages));
        }
        // ==============================================================
        // REVIEWS & REPLY ENDPOINTS (SHOPEE / TIKTOK STYLE)
        // ==============================================================
        // 1. Lấy danh sách reviews & thống kê của gói tập (Public)
        [HttpGet("{id}/reviews")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackageReviews(Guid id, CancellationToken cancellationToken)
        {
            var result = await _service.GetPackageReviewsAsync(id, cancellationToken);
            return Ok(result);
        }
        // 2. Trainee gửi đánh giá cho gói tập
        [HttpPost("{id}/reviews")]
        [Authorize(Roles = "TRAINEE")]
        public async Task<IActionResult> SubmitPackageReview(Guid id, [FromBody] CreatePackageReviewDto dto, CancellationToken cancellationToken)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var traineeId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Invalid token or user ID"));
            }
            var result = await _service.SubmitPackageReviewAsync(id, traineeId, dto, cancellationToken);
            return Ok(result);
        }
        // 3. Coach phản hồi lại đánh giá của học viên
        [HttpPost("{id}/reviews/{reviewId}/reply")]
        [Authorize(Roles = "COACH")]
        public async Task<IActionResult> ReplyToReview(Guid id, Guid reviewId, [FromBody] ReplyReviewDto dto, CancellationToken cancellationToken)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var coachId))
            {
                return Unauthorized(ApiResponseDto<bool>.Fail("Invalid token or user ID"));
            }
            var result = await _service.ReplyToReviewAsync(id, reviewId, coachId, dto, cancellationToken);
            return Ok(result);
        }
    }
}

