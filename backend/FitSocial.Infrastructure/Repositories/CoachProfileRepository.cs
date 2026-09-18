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

    public async Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(c => c.Coach).AsQueryable();
        // 1. Tìm ki?m theo tên / gi?i thi?u
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.ToLower().Trim();
            query = query.Where(c => (c.Coach.FullName != null && c.Coach.FullName.ToLower().Contains(keyword)) ||
                                     (c.Bio != null && c.Bio.ToLower().Contains(keyword)));
        }
        // 2. L?c theo n?m kinh nghi?m t?i thi?u
        if (minExperience.HasValue && minExperience.Value > 0)
        {
            query = query.Where(c => c.ExperienceYears >= minExperience.Value);
        }
        // 3. S?p x?p (Sort)
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query = sortBy.ToLower() switch
            {
                "exp_desc" => query.OrderByDescending(c => c.ExperienceYears),
                "exp_asc" => query.OrderBy(c => c.ExperienceYears),
                "name_asc" => query.OrderBy(c => c.Coach.FullName),
                "name_desc" => query.OrderByDescending(c => c.Coach.FullName),
                _ => query.OrderByDescending(c => c.ExperienceYears) // M?c ??nh
            };
        }
        else
        {
            // M?c ??nh n?u không truy?n gì thì x?p kinh nghi?m t? cao xu?ng th?p
            query = query.OrderByDescending(c => c.ExperienceYears);
        }
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
