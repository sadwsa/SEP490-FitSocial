using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoachesController : ControllerBase
{
    private readonly ICoachService _coachService;
    private readonly ICoachBankAccountService _bankAccountService;

    public CoachesController(ICoachService coachService, ICoachBankAccountService bankAccountService)
    {
        _coachService = coachService;
        _bankAccountService = bankAccountService;
    }

    /// <summary>
    /// Gets the top 3 (or specified count) coaches with the highest average rating in the system.
    /// Filtered to active/approved coaches only, sorted by average rating in descending order.
    /// Accessible publicly without authentication.
    /// </summary>
    [HttpGet("top")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<List<TopCoachDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopCoaches([FromQuery] int count = 3, CancellationToken cancellationToken = default)
    {
        var result = await _coachService.GetTopCoachesAsync(count, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a list of coaches with filtering and sorting.
    /// Accessible publicly.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllCoaches([FromQuery] string? searchKeyword, [FromQuery] int? minExperience, [FromQuery] string? sortBy)
    {
        var result = await _coachService.GetAllCoachesAsync(searchKeyword, minExperience, sortBy);
        return Ok(ApiResponseDto<IEnumerable<CoachListDto>>.Ok(result));
    }

    /// <summary>
    /// View detailed profile of a coach including locations, certificates, and packages.
    /// Accessible publicly.
    /// </summary>
    [HttpGet("{coachId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponseDto<CoachDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachDetails(Guid coachId, CancellationToken cancellationToken = default)
    {
        var result = await _coachService.GetCoachDetailsAsync(coachId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the authenticated coach's own profile.
    /// </summary>
    [HttpGet("profile/me")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachDetailDto>.Fail("Unauthorized"));
        }

        var result = await _coachService.GetCoachDetailsAsync(currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the authenticated coach's profile.
    /// </summary>
    [HttpPut("profile/me")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateCoachProfileDto dto, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachDetailDto>.Fail("Unauthorized"));
        }

        var result = await _coachService.UpdateCoachProfileAsync(currentUserId, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the list of bank accounts registered by the coach.
    /// </summary>
    [HttpGet("bank-accounts")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<List<CoachBankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBankAccounts(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<List<CoachBankAccountDto>>.Fail("Unauthorized"));
        }

        var result = await _bankAccountService.GetAccountsAsync(currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Adds a new bank account with encrypted account number.
    /// </summary>
    [HttpPost("bank-accounts")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachBankAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddBankAccount([FromBody] CreateCoachBankAccountDto dto, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachBankAccountDto>.Fail("Unauthorized"));
        }

        var result = await _bankAccountService.AddAccountAsync(currentUserId, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sets a bank account as default.
    /// </summary>
    [HttpPut("bank-accounts/{bankId:guid}/default")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultBankAccount(Guid bankId, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<bool>.Fail("Unauthorized"));
        }

        var result = await _bankAccountService.SetDefaultAsync(currentUserId, bankId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a bank account.
    /// </summary>
    [HttpDelete("bank-accounts/{bankId:guid}")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBankAccount(Guid bankId, CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<bool>.Fail("Unauthorized"));
        }

        var result = await _bankAccountService.DeleteAccountAsync(currentUserId, bankId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/reviews")]
    [Authorize(Roles = "TRAINEE")] // Chỉ Trainee mới được đánh giá
    public async Task<IActionResult> SubmitReview(Guid id, [FromBody] FitSocial.Application.DTOs.Coaches.CreateReviewDto dto)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var traineeId))
        {
            return Unauthorized(FitSocial.Application.DTOs.Common.ApiResponseDto<bool>.Fail("Invalid token or user ID"));
        }

        var result = await _coachService.SubmitReviewAsync(id, traineeId, dto);
        return Ok(result);
    }
    [HttpPut("reviews/{reviewId}")]
    [Authorize(Roles = "TRAINEE")]
    public async Task<IActionResult> UpdateReview(Guid reviewId, [FromBody] FitSocial.Application.DTOs.Coaches.UpdateReviewDto dto)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var traineeId)) return Unauthorized(FitSocial.Application.DTOs.Common.ApiResponseDto<bool>.Fail("Invalid token"));

        var result = await _coachService.UpdateReviewAsync(reviewId, traineeId, dto);
        return Ok(result);
    }

    [HttpDelete("reviews/{reviewId}")]
    [Authorize(Roles = "TRAINEE")]
    public async Task<IActionResult> DeleteReview(Guid reviewId)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdString, out var traineeId)) return Unauthorized(FitSocial.Application.DTOs.Common.ApiResponseDto<bool>.Fail("Invalid token"));

        var result = await _coachService.DeleteReviewAsync(reviewId, traineeId);
        return Ok(result);
    }

    /// <summary>
    /// UC_29: Get coach application requests for Staff and Admin.
    /// Supports basic viewing, searching, filtering, and pagination.
    /// </summary>
    [HttpGet("applications")]
    [HttpGet("~/api/admin/coach-applications")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationListResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoachApplications(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int? minExperience,
        [FromQuery] int? maxExperience,
        [FromQuery] string? sortBy,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _coachService.GetCoachApplicationsAsync(
            search, status, minExperience, maxExperience, sortBy, pageNumber, pageSize, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC_29: Get status counts of coach applications (Staff/Admin).
    /// </summary>
    [HttpGet("applications/status-counts")]
    [HttpGet("~/api/admin/coach-applications/status-counts")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationStatusCountsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoachApplicationStatusCounts(CancellationToken cancellationToken = default)
    {
        var result = await _coachService.GetCoachApplicationStatusCountsAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC_29: Get detailed coach application request for Staff and Admin review (read-only).
    /// </summary>
    [HttpGet("applications/{coachId:guid}")]
    [HttpGet("~/api/admin/coach-applications/{coachId:guid}")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCoachApplicationDetails(Guid coachId, CancellationToken cancellationToken = default)
    {
        var result = await _coachService.GetCoachApplicationDetailsAsync(coachId, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC_29.1: Approve a pending coach application (Staff/Admin).
    /// </summary>
    [HttpPost("applications/{coachId:guid}/approve")]
    [HttpPost("~/api/admin/coach-applications/{coachId:guid}/approve")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ApproveCoachApplication(
        Guid coachId,
        [FromBody] ApproveCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachApplicationDetailDto>.Fail("Unauthorized"));
        }

        var result = await _coachService.ApproveCoachApplicationAsync(coachId, currentUserId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC_29.1: Reject a pending coach application (Staff/Admin).
    /// </summary>
    [HttpPost("applications/{coachId:guid}/reject")]
    [HttpPost("~/api/admin/coach-applications/{coachId:guid}/reject")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RejectCoachApplication(
        Guid coachId,
        [FromBody] RejectCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachApplicationDetailDto>.Fail("Unauthorized"));
        }

        var result = await _coachService.RejectCoachApplicationAsync(coachId, currentUserId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// UC_29.2: Review (Approve/Reject) an individual certificate of a coach application (Staff/Admin).
    /// If all certificates are rejected (or none exist), the application is auto-rejected.
    /// If at least one is rejected and others are approved/pending, the application remains pending.
    /// </summary>
    [HttpPost("applications/{coachId:guid}/certificates/{certificateId:guid}/review")]
    [HttpPost("~/api/admin/coach-applications/{coachId:guid}/certificates/{certificateId:guid}/review")]
    [Authorize(Roles = "STAFF,ADMIN")]
    [ProducesResponseType(typeof(ApiResponseDto<CoachApplicationDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReviewCertificate(
        Guid coachId,
        Guid certificateId,
        [FromBody] ReviewCertificateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var currentUserId))
        {
            return Unauthorized(ApiResponseDto<CoachApplicationDetailDto>.Fail("Unauthorized"));
        }

        var result = await _coachService.ReviewCertificateAsync(coachId, certificateId, currentUserId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
    /// <summary>
    /// Looks up bank account holder name via NAPAS 24/7.
    /// </summary>
    [HttpGet("bank-accounts/lookup")]
    [Authorize(Roles = "COACH")]
    [ProducesResponseType(typeof(ApiResponseDto<string?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupBankAccount(
        [FromQuery] string bin,
        [FromQuery] string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var result = await _bankAccountService.LookupAccountNameAsync(bin, accountNumber, cancellationToken);
        return Ok(result);
    }

}
