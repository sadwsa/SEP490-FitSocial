using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IPriceRepository : IRepository<Price>
{
    /// <summary>Latest active price row (used for the coach activation fee).</summary>
    Task<Price?> GetActiveAsync(CancellationToken cancellationToken = default);
}
