using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ICoachProfileRepository : IRepository<CoachProfile>
{
    Task<CoachProfile?> FindWithDetailsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default);

    Task<List<CoachRatingSummary>> GetTopCoachesAsync(int count, CancellationToken cancellationToken = default);

    Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null, CancellationToken cancellationToken = default);

    Task<(List<CoachProfile> Items, int TotalCount)> GetPagedCoachApplicationsAsync(
        string? search = null,
        string? status = null,
        int? minExperience = null,
        int? maxExperience = null,
        string? sortBy = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetCoachApplicationStatusCountsAsync(CancellationToken cancellationToken = default);

    Task<CoachProfile?> GetCoachApplicationDetailsAsync(Guid coachId, CancellationToken cancellationToken = default);
}
