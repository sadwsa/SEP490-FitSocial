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
}
