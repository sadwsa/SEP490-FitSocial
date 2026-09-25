using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class TermsAndPolicyRepository : Repository<TermsAndPolicy>, ITermsAndPolicyRepository
{
    public TermsAndPolicyRepository(FitSocialDbContext dbContext) : base(dbContext) { }

    public Task<TermsAndPolicy?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        return DbSet.OrderByDescending(t => t.EffectiveDate).FirstOrDefaultAsync(cancellationToken);
    }
}
