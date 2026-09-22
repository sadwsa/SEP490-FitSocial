using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachEkycVerificationRepository : Repository<CoachEkycVerification>, ICoachEkycVerificationRepository
{
    public CoachEkycVerificationRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public Task<List<CoachEkycVerification>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet.Where(e => e.CoachId == coachId).ToListAsync(cancellationToken);
    }
}
