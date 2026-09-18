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

    public Task<List<User>> ListUsersForAdminAsync(string? search = null, string? role = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();

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

        return query
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
