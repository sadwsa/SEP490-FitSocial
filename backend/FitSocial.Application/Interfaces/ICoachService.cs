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
}
