using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface ILocationRepository : IRepository<Location>
{
    Task<List<Location>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<(List<Location> Items, int TotalCount)> ListLocationsAsync(
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
