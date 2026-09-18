using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Coaches;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.Exceptions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Services;

public class CoachService : ICoachService
{
    private readonly ICoachProfileRepository _coachProfiles;

    public CoachService(ICoachProfileRepository coachProfiles)
    {
        _coachProfiles = coachProfiles;
    }

    public async Task<ApiResponseDto<List<TopCoachDto>>> GetTopCoachesAsync(int count = 3, CancellationToken cancellationToken = default)
    {
        if (count < 1)
        {
            throw new ValidationException("Count must be greater than or equal to 1.");
        }

        // Enforce an upper bound if requested by query to protect against oversized requests
        if (count > 50)
        {
            count = 50;
        }

        var coachSummaries = await _coachProfiles.GetTopCoachesAsync(count, cancellationToken);

        var dtoList = coachSummaries.Select(c => new TopCoachDto
        {
            CoachId = c.CoachId,
            FullName = c.FullName,
            AvatarUrl = c.AvatarUrl,
            Bio = c.Bio,
            ExperienceYears = c.ExperienceYears,
            TotalReviews = c.TotalReviews,
            Rating = Math.Round(c.AverageRating, 1)
        }).ToList();

        return ApiResponseDto<List<TopCoachDto>>.Ok(dtoList, "Top coaches retrieved successfully.");
    }

    public async Task<IEnumerable<CoachListDto>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null)
    {
        var coaches = await _coachProfiles.GetAllCoachesWithDetailsAsync(searchKeyword, minExperience, sortBy);

        return coaches.Select(c => new CoachListDto
        {
            CoachId = c.CoachId,
            FullName = c.Coach?.FullName ?? "Unknown Coach",
            AvatarUrl = c.Coach?.AvatarUrl,
            ExperienceYears = c.ExperienceYears,
            Bio = c.Bio,
            CertificateUrl = c.CertificateUrl
        });
    }
}
