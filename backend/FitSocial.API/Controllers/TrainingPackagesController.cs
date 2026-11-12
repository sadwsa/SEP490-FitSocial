using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private readonly CloudinaryImageService _imageService;

        public TrainingPackagesController(ITrainingPackageService service, CloudinaryImageService imageService)
        {
            _service = service;
            _imageService = imageService;
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
        public async Task<IActionResult> GetPurchasedPackages(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var currentUserId))
            {
                return Unauthorized(ApiResponseDto<PagedResultDto<TrainingPackageResponseDto>>.Fail("Invalid token or user ID"));
            }

            var packages = await _service.GetPurchasedPackagesAsync(currentUserId, pageNumber, pageSize);
            return Ok(ApiResponseDto<PagedResultDto<TrainingPackageResponseDto>>.Ok(packages));
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
        [Authorize]
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

        // 2.1 Trainee upload ảnh đánh giá lên Cloudinary (trước khi gửi review)
        [HttpPost("{id}/upload-images")]
        [Authorize]
        public async Task<IActionResult> UploadReviewImages(Guid id, IFormFileCollection files, CancellationToken cancellationToken = default)
        {
            var uploadedUrls = await _imageService.UploadImagesFromFilesAsync(files, cancellationToken);
            return Ok(ApiResponseDto<List<string>>.Ok(uploadedUrls, "Images uploaded successfully."));
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
            try
            {
                var result = await _service.ReplyToReviewAsync(id, reviewId, coachId, dto, cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponseDto<bool>.Fail(ex.Message));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ApiResponseDto<bool>.Fail(ex.Message));
            }
        }
    }
}

