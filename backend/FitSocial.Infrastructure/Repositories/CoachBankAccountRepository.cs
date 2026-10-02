using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class CoachBankAccountRepository : Repository<CoachBankAccount>, ICoachBankAccountRepository
{
    public CoachBankAccountRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<CoachBankAccount>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(b => b.CoachId == coachId)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<CoachBankAccount?> GetDefaultByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(b => b.CoachId == coachId && b.IsDefault == true, cancellationToken);
    }
}
