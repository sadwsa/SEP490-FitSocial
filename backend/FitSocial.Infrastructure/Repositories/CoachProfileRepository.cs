using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachProfileRepository : Repository<CoachProfile>, ICoachProfileRepository
{
    public CoachProfileRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<CoachProfile?> FindWithSportsByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(p => p.Sports)
            .FirstOrDefaultAsync(p => p.CoachId == coachId, cancellationToken);
    }

    public async Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Coach)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
