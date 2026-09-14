using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Payment?> FindByGatewayTransactionIdAsync(string gatewayTransactionId, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(p => p.GatewayTransactionId == gatewayTransactionId, cancellationToken);
    }
}
