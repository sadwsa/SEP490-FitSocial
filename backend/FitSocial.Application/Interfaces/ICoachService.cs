using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;

namespace FitSocial.Application.Interfaces;

public interface ICoachService
{
    Task<ApiResponseDto<List<TopCoachDto>>> GetTopCoachesAsync(int count = 3, CancellationToken cancellationToken = default);
}
