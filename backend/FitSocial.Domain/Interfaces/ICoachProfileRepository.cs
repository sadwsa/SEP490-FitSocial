using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachProfileRepository : IRepository<CoachProfile>
{
    Task<CoachProfile?> FindWithSportsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<List<CoachRatingSummary>> GetTopCoachesAsync(int count, CancellationToken cancellationToken = default);

    Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null, CancellationToken cancellationToken = default);
}
