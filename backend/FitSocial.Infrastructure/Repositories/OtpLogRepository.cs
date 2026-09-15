using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class OtpLogRepository : Repository<Otplog>, IOtpLogRepository
{
    public OtpLogRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Otplog>> ListUnverifiedAsync(string normalizedEmail, string purpose, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(o => o.Email == normalizedEmail && o.Purpose == purpose && o.VerifiedAt == null)
            .ToListAsync(cancellationToken);
    }

    public Task<Otplog?> FindLatestUnverifiedAsync(string normalizedEmail, string purpose, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(o => o.Email == normalizedEmail && o.Purpose == purpose && o.VerifiedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
