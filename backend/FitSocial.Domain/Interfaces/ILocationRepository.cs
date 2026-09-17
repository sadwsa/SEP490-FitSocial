using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ILocationRepository : IRepository<Location>
{
    Task<List<Location>> ListAllAsync(CancellationToken cancellationToken = default);
}
