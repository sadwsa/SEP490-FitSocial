using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FitSocial.Application.Services;

public class CoachService : ICoachService
{
    private readonly ICoachProfileRepository _repository;

    public CoachService(ICoachProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<CoachListDto>> GetAllCoachesAsync()
    {
        var coaches = await _repository.GetAllCoachesWithDetailsAsync();

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
