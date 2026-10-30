using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Notification>> GetUnreadNotificationsByReferenceIdsAsync(Guid userId, IEnumerable<Guid> referenceIds, CancellationToken cancellationToken = default)
    {
        var refList = referenceIds.ToList();
        if (!refList.Any())
        {
            return new List<Notification>();
        }

        return await DbSet
            .Where(n => n.UserId == userId
                     && (n.IsRead == false || n.IsRead == null)
                     && n.ReferenceId.HasValue
                     && refList.Contains(n.ReferenceId.Value))
            .ToListAsync(cancellationToken);
    }
}
