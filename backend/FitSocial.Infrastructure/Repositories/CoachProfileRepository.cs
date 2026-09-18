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

    public async Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(c => c.Coach).AsQueryable();
        // 1. Tìm kiếm theo tên / giới thiệu
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.ToLower().Trim();
            query = query.Where(c => (c.Coach.FullName != null && c.Coach.FullName.ToLower().Contains(keyword)) ||
                                     (c.Bio != null && c.Bio.ToLower().Contains(keyword)));
        }
        // 2. Lọc theo năm kinh nghiệm tối thiểu
        if (minExperience.HasValue && minExperience.Value > 0)
        {
            query = query.Where(c => c.ExperienceYears >= minExperience.Value);
        }
        // 3. Sắp xếp (Sort)
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "exp_desc" => query.OrderByDescending(c => c.ExperienceYears),
                "exp_asc" => query.OrderBy(c => c.ExperienceYears),
                "name_asc" => query.OrderBy(c => c.Coach.FullName),
                "name_desc" => query.OrderByDescending(c => c.Coach.FullName),
                _ => query.OrderByDescending(c => c.ExperienceYears) // Mặc định
            };
        }
        else
        {
            // Mặc định nếu không truyền gì thì xếp kinh nghiệm từ cao xuống thấp
            query = query.OrderByDescending(c => c.ExperienceYears);
        }
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
