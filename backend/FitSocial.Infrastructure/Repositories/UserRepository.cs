using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<bool> ExistsByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<bool> ExistsByPhoneAsync(string phoneNumber, Guid? excludingUserId = null, CancellationToken cancellationToken = default)
    {
        return DbSet.AnyAsync(
            u => u.PhoneNumber == phoneNumber && (excludingUserId == null || u.UserId != excludingUserId),
            cancellationToken);
    }

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<User?> FindByGoogleSubAsync(string googleSub, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(u => u.GoogleProviderId == googleSub, cancellationToken);
    }

    public async Task<(List<User> Items, int TotalCount)> ListUsersForAdminAsync(
        string? search = null,
        string? role = null,
        bool? isLocked = null,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                (u.FullName != null && u.FullName.ToLower().Contains(s)) ||
                u.Email.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToUpper();
            query = query.Where(u => u.RoleCode == r);
        }

        if (isLocked.HasValue)
        {
            if (isLocked.Value)
            {
                query = query.Where(u => u.IsLocked == true);
            }
            else
            {
                query = query.Where(u => u.IsLocked == null || u.IsLocked == false);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<User?> FindWithCoachProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking()
            .Include(u => u.CoachProfileCoach)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public Task<User?> FindUserProfileByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return DbSet.AsNoTracking()
            .Include(u => u.CoachProfileCoach)
                .ThenInclude(cp => cp!.Locations)
            .Include(u => u.CoachProfileCoach)
                .ThenInclude(cp => cp!.Reviews)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }
}
