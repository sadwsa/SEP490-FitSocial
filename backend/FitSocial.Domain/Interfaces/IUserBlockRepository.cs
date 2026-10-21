using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface IUserBlockRepository : IRepository<UserBlock>
{
    Task<List<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken cancellationToken = default);
    Task<bool> IsBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
    Task<UserBlock?> GetBlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default);
}
