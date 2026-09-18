using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ISportRepository : IRepository<Sport>
{
    Task<List<Sport>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<List<Sport>> ListByIdsAsync(IEnumerable<Guid> sportIds, CancellationToken cancellationToken = default);
}
