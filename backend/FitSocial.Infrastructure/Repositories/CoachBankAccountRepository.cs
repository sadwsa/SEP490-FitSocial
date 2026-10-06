using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class CoachBankAccountRepository : Repository<CoachBankAccount>, ICoachBankAccountRepository
{
    public CoachBankAccountRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<CoachBankAccount>> ListByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(b => b.CoachId == coachId)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<CoachBankAccount?> GetDefaultByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .FirstOrDefaultAsync(b => b.CoachId == coachId && b.IsDefault == true, cancellationToken);
    }

    public Task<List<CoachBankAccount>> ListActiveByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(b => b.CoachId == coachId && b.IsActive != false)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<CoachBankAccount>> ListAllWithCoachAsync(string? searchTerm, string? bankCode, bool? defaultOnly, CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(b => b.Coach)
                .ThenInclude(c => c.Coach)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(bankCode))
        {
            var code = bankCode.Trim().ToUpperInvariant();
            query = query.Where(b => b.BankCode != null && b.BankCode.ToUpper() == code);
        }

        if (defaultOnly == true)
        {
            query = query.Where(b => b.IsDefault == true);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(b =>
                (b.AccountName != null && b.AccountName.ToLower().Contains(term)) ||
                (b.BankName != null && b.BankName.ToLower().Contains(term)) ||
                (b.Coach != null && b.Coach.Coach != null && (
                    (b.Coach.Coach.FullName != null && b.Coach.Coach.FullName.ToLower().Contains(term)) ||
                    (b.Coach.Coach.Email != null && b.Coach.Coach.Email.ToLower().Contains(term)))));
        }

        return query
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
