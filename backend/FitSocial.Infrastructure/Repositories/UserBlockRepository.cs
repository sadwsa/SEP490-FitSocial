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

public class UserBlockRepository : Repository<UserBlock>, IUserBlockRepository
{
    public UserBlockRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(b => b.BlockerId == blockerId)
            .Select(b => b.BlockedId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsBlockedAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);
    }

    public async Task<UserBlock?> GetBlockAsync(Guid blockerId, Guid blockedId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId, cancellationToken);
    }
}
