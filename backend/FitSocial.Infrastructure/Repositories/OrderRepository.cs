using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Order>> ListPendingActivationByUserAsync(Guid userId, string orderType, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(o => o.TraineeId == userId
                && o.OrderType == orderType
                && o.OrderStatus == Domain.Constants.PaymentConstants.OrderStatusPending)
            .ToListAsync(cancellationToken);
    }
}
