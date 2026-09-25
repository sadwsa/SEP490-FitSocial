using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class UserAgreementRepository : Repository<UserAgreement>, IUserAgreementRepository
{
    public UserAgreementRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public Task<UserAgreement?> FindByUserAndTermAsync(Guid userId, Guid termId, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.TermId == termId, cancellationToken);
    }
}
