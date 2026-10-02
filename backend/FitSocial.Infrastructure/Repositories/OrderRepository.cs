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
            .Where(o => o.BuyerId == userId
                && o.OrderType == orderType
                && o.OrderStatus == Domain.Constants.PaymentConstants.OrderStatusPending)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Order>> GetOrdersByBuyerIdAsync(Guid buyerId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(o => o.Buyer)
            .Include(o => o.Coach)
                .ThenInclude(c => c!.Coach)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Package)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.CoachSubscriptionPlan)
            .Include(o => o.Payments)
            .Where(o => o.BuyerId == buyerId)
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Include(o => o.Buyer)
            .Include(o => o.Coach)
                .ThenInclude(c => c!.Coach)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Package)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.CoachSubscriptionPlan)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }
}
