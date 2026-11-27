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
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.TrainingPlans)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.MealPlans)
            .Include(o => o.RefundRequests)
            .Include(o => o.Payments)
            .Include(o => o.PayoutItems)
                .ThenInclude(pi => pi.Payout)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<bool> HasSoldTrainingPackageAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, Domain.Constants.PaymentConstants.OrderStatusActive, Domain.Constants.PaymentConstants.OrderStatusCompleted };

        return await DbSet
            .AsNoTracking()
            .AnyAsync(o =>
                (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                && o.OrderStatus != null
                && paidStatuses.Contains(o.OrderStatus.ToUpper()),
                cancellationToken);
    }

    public async Task<HashSet<Guid>> GetCoachIdsWithSoldPackagesAsync(IEnumerable<Guid> coachIds, CancellationToken cancellationToken = default)
    {
        var idSet = coachIds.Distinct().ToList();
        if (idSet.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, Domain.Constants.PaymentConstants.OrderStatusActive, Domain.Constants.PaymentConstants.OrderStatusCompleted };

        var matchingOrders = await DbSet
            .AsNoTracking()
            .Where(o => o.OrderStatus != null && paidStatuses.Contains(o.OrderStatus.ToUpper()))
            .Select(o => new
            {
                o.CoachId,
                PackageCoachIds = o.OrderDetails
                    .Where(od => od.Package != null && idSet.Contains(od.Package.CoachId))
                    .Select(od => od.Package!.CoachId)
            })
            .Where(o => (o.CoachId.HasValue && idSet.Contains(o.CoachId.Value)) || o.PackageCoachIds.Any())
            .ToListAsync(cancellationToken);

        var result = new HashSet<Guid>();
        foreach (var o in matchingOrders)
        {
            if (o.CoachId.HasValue && idSet.Contains(o.CoachId.Value))
            {
                result.Add(o.CoachId.Value);
            }
            foreach (var pId in o.PackageCoachIds)
            {
                result.Add(pId);
            }
        }

        return result;
    }


    public async Task<List<Order>> GetOrdersByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, Domain.Constants.PaymentConstants.OrderStatusActive, Domain.Constants.PaymentConstants.OrderStatusCompleted };

        return await DbSet
            .Include(o => o.Buyer)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Package)
                    .ThenInclude(p => p!.Media)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.TrainingPlans)
                    .ThenInclude(tp => tp.TrainingPlanExercises)
                        .ThenInclude(e => e.WorkoutCompletionLogs)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.MealPlans)
                    .ThenInclude(mp => mp.MealPlanItems)
                        .ThenInclude(i => i.MealCompletionLogs)
            .Include(o => o.Payments)
            .Where(o => (o.CoachId == coachId || o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == coachId))
                && o.OrderType == Domain.Constants.PaymentConstants.OrderTypePackage
                && o.OrderStatus != null
                && paidStatuses.Contains(o.OrderStatus.ToUpper()))
            .OrderByDescending(o => o.CreatedAt)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public Task<List<Order>> GetCompletedOrdersByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
    {
        var paidStatuses = new[] { Domain.Constants.PaymentConstants.OrderStatusPaid, Domain.Constants.PaymentConstants.OrderStatusActive, Domain.Constants.PaymentConstants.OrderStatusCompleted };

        return DbSet
            .Where(o => o.CoachId == coachId
                     && o.OrderStatus != null
                     && paidStatuses.Contains(o.OrderStatus.ToUpper()))
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

}

