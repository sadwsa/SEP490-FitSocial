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

    public Task<bool> ExistsByIdCardNumberAsync(string idCardNumber, Guid? excludeCoachId = null, CancellationToken cancellationToken = default)
    {
        var q = DbSet.Where(e => e.IdCardNumber == idCardNumber);
        if (excludeCoachId.HasValue) q = q.Where(e => e.CoachId != excludeCoachId.Value);
        return q.AnyAsync(cancellationToken);
    }
}
