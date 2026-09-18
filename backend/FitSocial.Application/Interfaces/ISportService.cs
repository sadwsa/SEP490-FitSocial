using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;

namespace FitSocial.Application.Interfaces;

public interface ISportService
{
    Task<ApiResponseDto<List<SportDto>>> GetSportListAsync(CancellationToken cancellationToken = default);
    Task<ApiResponseDto<SportDto>> CreateSportAsync(CreateSportDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> DeleteSportAsync(Guid sportId, CancellationToken cancellationToken = default);
}
