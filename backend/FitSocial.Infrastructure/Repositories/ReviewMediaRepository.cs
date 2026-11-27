using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class ReviewMediaRepository : Repository<ReviewMedia>, IReviewMediaRepository
{
    public ReviewMediaRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<List<ReviewMedia>> GetMediaByReviewIdAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(m => m.ReviewId == reviewId)
            .OrderBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ReviewMedia>> GetMediaByPackageIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(m => m.Review)
            .Where(m => m.Review.PackageId == packageId)
            .OrderBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
