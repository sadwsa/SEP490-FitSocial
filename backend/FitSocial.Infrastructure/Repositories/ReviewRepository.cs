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

public class ReviewRepository : Repository<Review>, IReviewRepository
{
    public ReviewRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<Review>> GetReviewsByPackageIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(r => r.Trainee)
            .Include(r => r.Coach)
                .ThenInclude(c => c.Coach)
            .Where(r => r.PackageId == packageId)
            .OrderByDescending(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Review?> GetReviewByPackageAndTraineeAsync(Guid packageId, Guid traineeId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(r => r.PackageId == packageId && r.TraineeId == traineeId, cancellationToken);
    }
}
