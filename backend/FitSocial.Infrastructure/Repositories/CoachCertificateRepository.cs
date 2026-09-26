using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class CoachCertificateRepository : Repository<CoachCertificate>, ICoachCertificateRepository
{
    public CoachCertificateRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public Task<List<CoachCertificate>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet.Where(c => c.CoachId == coachId).ToListAsync(cancellationToken);
    }
}
