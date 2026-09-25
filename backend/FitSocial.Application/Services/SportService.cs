using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Sports;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Services;

public class SportService : ISportService
{
    private readonly ISportRepository _sportRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SportService(ISportRepository sportRepository, IUnitOfWork unitOfWork)
    {
        _sportRepository = sportRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponseDto<List<SportDto>>> GetSportListAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sports = await _sportRepository.ListAllWithCountsAsync(cancellationToken);

            var dtos = sports.Select((s, index) => MapToDto(s, index + 1)).ToList();

            return ApiResponseDto<List<SportDto>>.Ok(dtos, "Sports list retrieved successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<List<SportDto>>.Fail($"Failed to retrieve sports list: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<SportDto>> CreateSportAsync(CreateSportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.SportName))
        {
            return ApiResponseDto<SportDto>.Fail("Sport name cannot be empty.");
        }

        var trimmedName = dto.SportName.Trim();
        if (trimmedName.Length > 100)
        {
            return ApiResponseDto<SportDto>.Fail("Sport name cannot exceed 100 characters.");
        }

        try
        {
            var existingSport = await _sportRepository.GetByNameAsync(trimmedName, cancellationToken);
            if (existingSport != null)
            {
                return ApiResponseDto<SportDto>.Fail($"Sport '{trimmedName}' already exists.");
            }

            var newSport = new Sport
            {
                SportId = Guid.NewGuid(),
                SportName = trimmedName
            };

            await _sportRepository.AddAsync(newSport, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = MapToDto(newSport, 1);
            return ApiResponseDto<SportDto>.Ok(resultDto, $"Sport '{trimmedName}' created successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<SportDto>.Fail($"Error creating sport: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<SportDto>> UpdateSportAsync(Guid sportId, UpdateSportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.SportName))
        {
            return ApiResponseDto<SportDto>.Fail("Sport name cannot be empty.");
        }

        var trimmedName = dto.SportName.Trim();
        if (trimmedName.Length > 100)
        {
            return ApiResponseDto<SportDto>.Fail("Sport name cannot exceed 100 characters.");
        }

        try
        {
            var sport = await _sportRepository.GetByIdAsync(sportId, cancellationToken);
            if (sport == null)
            {
                return ApiResponseDto<SportDto>.Fail("Sport not found.");
            }

            var duplicate = await _sportRepository.GetByNameExcludingIdAsync(trimmedName, sportId, cancellationToken);
            if (duplicate != null)
            {
                return ApiResponseDto<SportDto>.Fail($"Sport '{trimmedName}' already exists.");
            }

            sport.SportName = trimmedName;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var resultDto = MapToDto(sport, 1);
            return ApiResponseDto<SportDto>.Ok(resultDto, "Sport updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<SportDto>.Fail($"Error updating sport: {ex.Message}");
        }
    }

    public async Task<ApiResponseDto<bool>> DeleteSportAsync(Guid sportId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sport = await _sportRepository.GetByIdWithDetailsAsync(sportId, cancellationToken);
            if (sport == null)
            {
                return ApiResponseDto<bool>.Fail("Sport not found.");
            }

            if ((sport.Coaches != null && sport.Coaches.Count > 0) ||
                (sport.Users != null && sport.Users.Count > 0))

            {
                return ApiResponseDto<bool>.Fail("Cannot delete this sport because it is currently linked to coaches or users.");
            }

            _sportRepository.Remove(sport);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponseDto<bool>.Ok(true, "Sport deleted successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.Fail($"Error deleting sport: {ex.Message}");
        }
    }

    private static SportDto MapToDto(Sport s, int index)
    {
        return new SportDto
        {
            SportId = s.SportId,
            SportName = s.SportName,
            Code = $"SPORT_{index:00}",
            Category = DetermineCategory(s.SportName),
            Description = $"Official FitSocial training track for {s.SportName}.",
            Metrics = DetermineMetrics(s.SportName),
            Icon = DetermineIcon(s.SportName),
            CoachCount = s.Coaches?.Count ?? 0,
            UserCount = s.Users?.Count ?? 0,
            PostCount = 0,
            Visible = true
        };
    }

    private static string DetermineCategory(string sportName)
    {
        if (string.IsNullOrWhiteSpace(sportName)) return "Gym & Strength";

        var name = sportName.ToLowerInvariant();
        if (name.Contains("run") || name.Contains("marathon") || name.Contains("cardio") || name.Contains("swim") || name.Contains("cycle") || name.Contains("bÆ¡i") || name.Contains("cháº¡y") || name.Contains("Ä‘áº¡p"))
            return "Cardio & Athletics";
        if (name.Contains("box") || name.Contains("mma") || name.Contains("karate") || name.Contains("judo") || name.Contains("vÃµ") || name.Contains("muay") || name.Contains("kick"))
            return "Combat Sports";
        if (name.Contains("foot") || name.Contains("basket") || name.Contains("volley") || name.Contains("soccer") || name.Contains("bÃ³ng") || name.Contains("tennis") || name.Contains("cáº§u"))
            return "Team Sports";

        return "Gym & Strength";
    }

    private static string DetermineMetrics(string sportName)
    {
        var name = sportName.ToLowerInvariant();
        if (name.Contains("run") || name.Contains("marathon") || name.Contains("cháº¡y") || name.Contains("Ä‘áº¡p"))
            return "GPS Route, Pace, Km";
        if (name.Contains("swim") || name.Contains("bÆ¡i"))
            return "Laps, Pace, Distance";
        if (name.Contains("box") || name.Contains("mma") || name.Contains("vÃµ"))
            return "Rounds, Heart rate";
        if (name.Contains("foot") || name.Contains("basket") || name.Contains("bÃ³ng"))
            return "Goals, Points, Assists";

        return "Weight/Kg, Reps/Sets";
    }

    private static string DetermineIcon(string sportName)
    {
        var name = sportName.ToLowerInvariant();
        if (name.Contains("run") || name.Contains("cháº¡y") || name.Contains("cardio") || name.Contains("yoga"))
            return "activity";
        if (name.Contains("swim") || name.Contains("bÆ¡i"))
            return "waves";
        if (name.Contains("box") || name.Contains("mma") || name.Contains("vÃµ"))
            return "fire";
        if (name.Contains("foot") || name.Contains("basket") || name.Contains("bÃ³ng") || name.Contains("tennis"))
            return "ball";

        return "dumbbell";
    }
}
