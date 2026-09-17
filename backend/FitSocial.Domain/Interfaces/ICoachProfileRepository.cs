using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachProfileRepository : IRepository<CoachProfile>
{
    Task<CoachProfile?> FindWithSportsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(CancellationToken cancellationToken = default);
}
