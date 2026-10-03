using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FitSocial.Infrastructure.Repositories;

public class PayoutRepository : Repository<Payout>, IPayoutRepository
{
    private readonly FitSocialDbContext _ctx;

    public PayoutRepository(FitSocialDbContext dbContext) : base(dbContext)
    {
        _ctx = dbContext;
    }

    public Task<Payout?> GetByIdWithDetailsAsync(Guid payoutId, CancellationToken cancellationToken = default)
    {
        return _ctx.Payouts
            .Include(p => p.Coach)
                .ThenInclude(c => c!.Coach)
            .Include(p => p.ProcessedByNavigation)
            .Include(p => p.PayoutItems)
                .ThenInclude(i => i.Order)
                    .ThenInclude(o => o!.Buyer)
            .Include(p => p.PayoutItems)
                .ThenInclude(i => i.Order)
                    .ThenInclude(o => o!.OrderDetails)
            .FirstOrDefaultAsync(p => p.PayoutId == payoutId, cancellationToken);
    }

    public Task<List<Payout>> ListHistoryAsync(int? month, int? year, string? status, Guid? coachId, CancellationToken cancellationToken = default)
    {
        var query = _ctx.Payouts
            .Include(p => p.Coach)
                .ThenInclude(c => c!.Coach)
            .Include(p => p.ProcessedByNavigation)
            .Include(p => p.PayoutItems)
            .AsQueryable();

        if (month.HasValue) query = query.Where(p => p.PayoutMonth == month.Value);
        if (year.HasValue) query = query.Where(p => p.PayoutYear == year.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        if (coachId.HasValue && coachId.Value != Guid.Empty) query = query.Where(p => p.CoachId == coachId.Value);

        return query
            .OrderByDescending(p => p.PayoutYear)
            .ThenByDescending(p => p.PayoutMonth)
            .ThenByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Payout?> FindCycleAsync(Guid coachId, int month, int year, CancellationToken cancellationToken = default)
    {
        return _ctx.Payouts
            .Include(p => p.PayoutItems)
            .FirstOrDefaultAsync(p => p.CoachId == coachId && p.PayoutMonth == month && p.PayoutYear == year, cancellationToken);
    }

    public void RemoveItems(IEnumerable<PayoutItem> items)
    {
        _ctx.PayoutItems.RemoveRange(items);
    }

    private static bool IsCompletedSale(Order o)
    {
        if (o.OrderType != null && !string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var s = o.OrderStatus?.ToUpperInvariant();
        return s == "PAID" || s == "COMPLETED" || s == "ACTIVE";
    }

    public async Task<List<Order>> ListCompletedOrdersForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var candidates = await _ctx.Orders
            .Include(o => o.Buyer)
            .Include(o => o.OrderDetails)
            .Where(o => o.CoachId == coachId
                && o.CreatedAt.HasValue
                && o.CreatedAt.Value >= from
                && o.CreatedAt.Value < to)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return candidates.Where(IsCompletedSale).ToList();
    }

    public Task<List<RefundRequest>> ListApprovedRefundsForCycleAsync(Guid coachId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return _ctx.RefundRequests
            .Include(r => r.Order)
            .Where(r => r.Status != null
                && r.Status.ToUpper() == "APPROVED"
                && r.Order != null
                && r.Order.CoachId == coachId
                && ((r.RefundedAt.HasValue && r.RefundedAt.Value >= from && r.RefundedAt.Value < to)
                    || (!r.RefundedAt.HasValue && r.ReviewedAt.HasValue && r.ReviewedAt.Value >= from && r.ReviewedAt.Value < to)))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> ListCoachesWithSalesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var candidates = await _ctx.Orders
            .Where(o => o.CoachId.HasValue
                && o.CreatedAt.HasValue
                && o.CreatedAt.Value >= from
                && o.CreatedAt.Value < to)
            .Select(o => new { o.CoachId, o.OrderType, o.OrderStatus })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return candidates
            .Where(o => (o.OrderType == null || string.Equals(o.OrderType, PaymentConstants.OrderTypePackage, StringComparison.OrdinalIgnoreCase))
                && o.OrderStatus != null
                && (o.OrderStatus.ToUpper() == "PAID" || o.OrderStatus.ToUpper() == "COMPLETED" || o.OrderStatus.ToUpper() == "ACTIVE"))
            .Select(o => o.CoachId!.Value)
            .Distinct()
            .ToList();
    }
}
