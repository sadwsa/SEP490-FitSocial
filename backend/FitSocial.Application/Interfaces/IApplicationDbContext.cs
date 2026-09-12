using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Otplog> Otplogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
