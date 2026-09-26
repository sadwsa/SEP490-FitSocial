using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;

namespace FitSocial.Application.Interfaces;

public interface ILocationService
{
    Task<ApiResponseDto<PagedResultDto<LocationDto>>> GetPagedLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<PagedResultDto<LocationDto>>> GetLocationsAsync(
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<List<LocationDto>>> GetPublicLocationsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<LocationDto>> CreateLocationAsync(
        CreateLocationDto dto,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<LocationDto>> UpdateLocationAsync(
        Guid locationId,
        UpdateLocationDto dto,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<bool>> DeleteLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);
}
