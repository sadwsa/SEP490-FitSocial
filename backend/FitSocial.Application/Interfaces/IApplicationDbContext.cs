using FitSocial.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Otplog> Otplogs { get; }
    DbSet<Sport> Sports { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderDetail> OrderDetails { get; }
    DbSet<Payment> Payments { get; }
    DbSet<CoachProfile> CoachProfiles { get; }
    public DbSet<Price> Prices { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
