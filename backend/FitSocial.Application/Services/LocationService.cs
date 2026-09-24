using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Locations;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _locationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LocationService(ILocationRepository locationRepository, IUnitOfWork unitOfWork)
    {
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<PagedResultDto<LocationDto>>> GetPagedLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _locationRepository.ListLocationsAsync(
                searchTerm, pageNumber, pageSize, cancellationToken);

            var dtos = items.Select(l => new LocationDto
            {
                LocationId = l.LocationId,
                LocationName = l.LocationName,
                Address = l.Address
            }).ToList();

            var pagedResult = PagedResultDto<LocationDto>.Create(dtos, totalCount, pageNumber, pageSize);
            return ApiResponseDto<PagedResultDto<LocationDto>>.Ok(pagedResult, "Locations list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<PagedResultDto<LocationDto>>.Fail($"Failed to retrieve locations: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<List<LocationDto>>> GetPublicLocationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var locations = await _locationRepository.ListAllAsync(cancellationToken);

            var dtos = locations.Select(l => new LocationDto
            {
                LocationId = l.LocationId,
                LocationName = l.LocationName,
                Address = l.Address
            }).ToList();

            return ApiResponseDto<List<LocationDto>>.Ok(dtos, "Locations list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<LocationDto>>.Fail($"Failed to retrieve public locations: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<LocationDto>> CreateLocationAsync(
        CreateLocationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto == null)
        {
            return ApiResponseDto<LocationDto>.Fail("Request payload cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(dto.LocationName))
        {
            return ApiResponseDto<LocationDto>.Fail("Location name cannot be empty.");
        }

        var trimmedName = dto.LocationName.Trim();
        if (trimmedName.Length > 200)
        {
            return ApiResponseDto<LocationDto>.Fail("Location name cannot exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(dto.Address))
        {
            return ApiResponseDto<LocationDto>.Fail("Address cannot be empty.");
        }

        var trimmedAddress = dto.Address.Trim();
        if (trimmedAddress.Length > 200)
        {
            return ApiResponseDto<LocationDto>.Fail("Address cannot exceed 200 characters.");
        }

        try
        {
            var existingLocation = await _locationRepository.GetByNameAsync(trimmedName, cancellationToken);
            if (existingLocation != null)
            {
                return ApiResponseDto<LocationDto>.Fail($"Location '{trimmedName}' already exists.");
            }

            var newLocation = new Location
            {
                LocationId = Guid.NewGuid(),
                LocationName = trimmedName,
                Address = trimmedAddress
            };

            await _locationRepository.AddAsync(newLocation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = new LocationDto
            {
                LocationId = newLocation.LocationId,
                LocationName = newLocation.LocationName,
                Address = newLocation.Address
            };

            return ApiResponseDto<LocationDto>.Ok(resultDto, "Location created successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<LocationDto>.Fail($"Failed to create location: {ex.Message}");
        }
    }
}
