using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachProfileRepository : IRepository<CoachProfile>
{
    Task<CoachProfile?> FindWithSportsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<List<CoachRatingSummary>> GetTopCoachesAsync(int count, CancellationToken cancellationToken = default);
}
