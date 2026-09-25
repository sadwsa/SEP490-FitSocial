using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;

namespace FitSocial.Application.Interfaces;

public interface ILocationService
{
    Task<ApiResponseDto<PagedResultDto<LocationDto>>> GetLocationsAsync(
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<List<LocationDto>>> GetPublicLocationsAsync(CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> DeleteLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
}
