using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Coaches;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Coaches;

public interface ICoachService
{
    Task<ApiResponse<IEnumerable<CoachListDto>>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null);
    Task<ApiResponse<List<TopCoachDto>>> GetTopCoachesAsync(int count = 5);
    Task<FitSocial.Client.Models.Common.ApiResponse<bool>> SubmitReviewAsync(Guid coachId, FitSocial.Client.Models.Coaches.CreateReviewDto dto);
    Task<FitSocial.Client.Models.Common.ApiResponse<bool>> UpdateReviewAsync(Guid reviewId, FitSocial.Client.Models.Coaches.UpdateReviewDto dto);

    Task<FitSocial.Client.Models.Common.ApiResponse<bool>> DeleteReviewAsync(Guid reviewId);

    Task<ApiResponse<CoachApplicationListResponseDto>> GetCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10);

    Task<ApiResponse<CoachApplicationDetailDto>> GetCoachApplicationDetailsAsync(Guid coachId);

    Task<ApiResponse<CoachApplicationStatusCountsDto>> GetCoachApplicationStatusCountsAsync();
}
