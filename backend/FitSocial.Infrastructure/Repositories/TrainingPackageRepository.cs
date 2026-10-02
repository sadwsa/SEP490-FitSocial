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

        public async Task<IEnumerable<TrainingPackage>> GetAllActivePackagesAsync(
            string? searchKeyword = null,
            decimal? maxPrice = null,
            Guid? coachId = null,
            CancellationToken cancellationToken = default)
        {
            var query = DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Where(x => x.IsActive == true);

            if (coachId.HasValue && coachId.Value != Guid.Empty)
            {
                query = query.Where(x => x.CoachId == coachId.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(x => x.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(x => (x.Title != null && x.Title.ToLower().Contains(term)) ||
                                         (x.Description != null && x.Description.ToLower().Contains(term)) ||
                                         (x.TargetAudience != null && x.TargetAudience.ToLower().Contains(term)) ||
                                         (x.Coach != null && x.Coach.Coach != null && x.Coach.Coach.FullName != null && x.Coach.Coach.FullName.ToLower().Contains(term)));
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<TrainingPackage>> GetPurchasedPackagesAsync(Guid traineeId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Where(x => x.OrderDetails.Any(od => od.Order.TraineeId == traineeId))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }
    }
}
