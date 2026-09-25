using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPriceRepository : IRepository<Price>
{
    /// <summary>Latest active price row (used for the coach activation fee).</summary>
    Task<Price?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<(List<Price> Items, int TotalCount)> ListPagedAsync(bool? isActive, string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
}
