using FitSocial.Domain.Entities;

namespace FitSocial.Domain.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task<List<Notification>> GetUnreadNotificationsByReferenceIdsAsync(Guid userId, IEnumerable<Guid> referenceIds, CancellationToken cancellationToken = default);
}
