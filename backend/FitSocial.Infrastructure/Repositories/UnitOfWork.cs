using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;

namespace FitSocial.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly FitSocialDbContext _dbContext;

    public UnitOfWork(FitSocialDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
