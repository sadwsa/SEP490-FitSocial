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

    public async Task<IEnumerable<CoachProfile>> GetAllCoachesWithDetailsAsync(string? searchKeyword = null, int? minExperience = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Include(c => c.Coach).AsQueryable();
        // L?c theo t? khóa (Tìm trong Tên ho?c Bio)
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var keyword = searchKeyword.ToLower().Trim();
            query = query.Where(c => (c.Coach.FullName != null && c.Coach.FullName.ToLower().Contains(keyword)) ||
                                     (c.Bio != null && c.Bio.ToLower().Contains(keyword)));
        }
        // L?c theo n?m kinh nghi?m t?i thi?u
        if (minExperience.HasValue && minExperience.Value > 0)
        {
            query = query.Where(c => c.ExperienceYears >= minExperience.Value);
        }
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
