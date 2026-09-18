using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ISportRepository : IRepository<Sport>
{
    Task<List<Sport>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<List<Sport>> ListAllWithCountsAsync(CancellationToken cancellationToken = default);
    Task<Sport?> GetByIdWithDetailsAsync(Guid sportId, CancellationToken cancellationToken = default);
    Task<Sport?> GetByNameAsync(string sportName, CancellationToken cancellationToken = default);
    Task<Sport?> GetByNameExcludingIdAsync(string sportName, Guid excludeSportId, CancellationToken cancellationToken = default);
    Task<List<Sport>> ListByIdsAsync(IEnumerable<Guid> sportIds, CancellationToken cancellationToken = default);
}
