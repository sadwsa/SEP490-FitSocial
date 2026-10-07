using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface ICoachService
{
    Task<ApiResponseDto<List<TopCoachDto>>> GetTopCoachesAsync(int count = 3, CancellationToken cancellationToken = default);

    Task<IEnumerable<CoachListDto>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null);

    Task<ApiResponseDto<CoachDetailDto>> GetCoachDetailsAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<CoachDetailDto>> UpdateCoachProfileAsync(Guid coachId, UpdateCoachProfileDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> SubmitReviewAsync(Guid coachId, Guid traineeId, FitSocial.Application.DTOs.Coaches.CreateReviewDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> UpdateReviewAsync(Guid reviewId, Guid traineeId, FitSocial.Application.DTOs.Coaches.UpdateReviewDto dto, CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> DeleteReviewAsync(Guid reviewId, Guid traineeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_29: Get paginated coach application requests with searching, filtering, and status counts for Staff/Admin.
    /// </summary>
    Task<ApiResponseDto<CoachApplicationListResponseDto>> GetCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_29: Get full details of a specific coach application request for Staff/Admin review (read-only).
    /// </summary>
    Task<ApiResponseDto<CoachApplicationDetailDto>> GetCoachApplicationDetailsAsync(
        Guid coachId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_29: Get summary counts of coach applications by status for Staff/Admin tabs.
    /// </summary>
    Task<ApiResponseDto<CoachApplicationStatusCountsDto>> GetCoachApplicationStatusCountsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_29.1: Approve a pending coach application (Staff/Admin).
    /// </summary>
    Task<ApiResponseDto<CoachApplicationDetailDto>> ApproveCoachApplicationAsync(
        Guid coachId,
        Guid approverId,
        ApproveCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC_29.1: Reject a pending coach application (Staff/Admin).
    /// </summary>
    Task<ApiResponseDto<CoachApplicationDetailDto>> RejectCoachApplicationAsync(
        Guid coachId,
        Guid rejectorId,
        RejectCoachApplicationRequestDto? request = null,
        CancellationToken cancellationToken = default);
}
