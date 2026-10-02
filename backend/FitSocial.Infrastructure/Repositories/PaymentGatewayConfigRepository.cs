using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PaymentGatewayConfigRepository : Repository<PaymentGatewayConfig>, IPaymentGatewayConfigRepository
{
    public PaymentGatewayConfigRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<PaymentGatewayConfig?> GetActiveByNameAsync(string gatewayName, CancellationToken cancellationToken = default)
    {
        var normalized = gatewayName.Trim().ToUpperInvariant();
        return DbSet
            .Where(c => c.IsActive == true && c.GatewayName != null && c.GatewayName.ToUpper() == normalized)
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<PaymentGatewayConfig>> GetAllConfigsAsync(CancellationToken cancellationToken = default)
    {
        return DbSet
            .OrderByDescending(c => c.UpdatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
