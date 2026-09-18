using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories
{
    public class TrainingPackageRepository : Repository<TrainingPackage>, ITrainingPackageRepository
    {
        public TrainingPackageRepository(FitSocialDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IEnumerable<TrainingPackage>> GetPackagesByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Where(x => x.CoachId == coachId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public override async Task<TrainingPackage?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .FirstOrDefaultAsync(x => x.PackageId == (Guid)id, cancellationToken);
        }
    }
}
