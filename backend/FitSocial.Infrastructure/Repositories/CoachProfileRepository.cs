using FitSocial.Domain.Constants;
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

    public async Task<List<CoachRatingSummary>> GetTopCoachesAsync(int count, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(cp => (cp.Coach.IsLocked == null || cp.Coach.IsLocked == false)
                      && cp.Coach.RoleCode != null
                      && cp.Coach.RoleCode.ToUpper() == RoleConstants.Coach
                      && (cp.ApprovalStatus == null || cp.ApprovalStatus.ToUpper() == "APPROVED"))
            .Select(cp => new CoachRatingSummary
            {
                CoachId = cp.CoachId,
                FullName = cp.Coach.FullName ?? string.Empty,
                AvatarUrl = cp.Coach.AvatarUrl,
                Bio = cp.Bio,
                ExperienceYears = cp.ExperienceYears,
                TotalReviews = cp.Reviews.Count(r => r.Rating != null),
                AverageRating = cp.Reviews.Where(r => r.Rating != null).Select(r => (double?)r.Rating).Average() ?? 0.0
            })
            .OrderByDescending(c => c.AverageRating)
            .ThenByDescending(c => c.TotalReviews)
            .ThenBy(c => c.CoachId)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
