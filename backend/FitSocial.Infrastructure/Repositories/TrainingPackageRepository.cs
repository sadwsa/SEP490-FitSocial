using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories
{
    public class TrainingPackageRepository : Repository<TrainingPackage>, ITrainingPackageRepository
    {
        public TrainingPackageRepository(FitSocialDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<IEnumerable<TrainingPackage>> GetPackagesByCoachIdAsync(Guid coachId, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Include(x => x.Media)
                .Where(x => x.CoachId == coachId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public override async Task<TrainingPackage?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Include(x => x.Coach)
                    .ThenInclude(c => c.CoachCertificates)
                .Include(x => x.Media)
                .FirstOrDefaultAsync(x => x.PackageId == (Guid)id, cancellationToken);
        }

        public async Task<IEnumerable<TrainingPackage>> GetAllActivePackagesAsync(
            string? searchKeyword = null,
            decimal? maxPrice = null,
            Guid? coachId = null,
            CancellationToken cancellationToken = default)
        {
            var query = DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Include(x => x.Media)
                .Where(x => x.IsActive == true);

            if (coachId.HasValue && coachId.Value != Guid.Empty)
            {
                query = query.Where(x => x.CoachId == coachId.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                query = query.Where(x => x.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(x => (x.Title != null && x.Title.ToLower().Contains(term)) ||
                                         (x.Description != null && x.Description.ToLower().Contains(term)) ||
                                         (x.TargetAudience != null && x.TargetAudience.ToLower().Contains(term)) ||
                                         (x.Coach != null && x.Coach.Coach != null && x.Coach.Coach.FullName != null && x.Coach.Coach.FullName.ToLower().Contains(term)));
            }

            return await query
                .OrderByDescending(x => x.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<List<TrainingPackage>> GetPurchasedPackagesAsync(Guid traineeId, CancellationToken cancellationToken = default)
        {
            var paidStatuses = new[]
            {
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusPaid,
                "PAID",
                "ACTIVE",
                "COMPLETED",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusRefundPending,
                "REFUND_PENDING",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusRefunded,
                "REFUNDED"
            };

            return await DbSet
                .Include(x => x.Coach)
                    .ThenInclude(c => c.Coach)
                .Include(x => x.Media)
                .Include(x => x.Reviews.Where(r => r.TraineeId == traineeId))
                .Include(x => x.OrderDetails.Where(od => od.Order.BuyerId == traineeId))
                    .ThenInclude(od => od.Order)
                        .ThenInclude(o => o.RefundRequests)
                .Include(x => x.OrderDetails.Where(od => od.Order.BuyerId == traineeId))
                    .ThenInclude(od => od.TrainingPlans)
                .Include(x => x.OrderDetails.Where(od => od.Order.BuyerId == traineeId))
                    .ThenInclude(od => od.MealPlans)
                .Where(x => x.OrderDetails.Any(od =>
                    od.Order.BuyerId == traineeId &&
                    od.Order.OrderStatus != null &&
                    paidStatuses.Contains(od.Order.OrderStatus.ToUpper())))
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
        }

        public async Task<(List<OrderDetail> Items, int TotalCount)> GetPurchasedOrderDetailsAsync(Guid traineeId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var paidStatuses = new[]
            {
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusPaid,
                "PAID",
                "ACTIVE",
                "COMPLETED",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusRefundPending,
                "REFUND_PENDING",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusRefunded,
                "REFUNDED"
            };

            var query = DbContext.Set<OrderDetail>()
                .Include(od => od.Order)
                    .ThenInclude(o => o.RefundRequests)
                .Include(od => od.Package)
                    .ThenInclude(p => p.Coach)
                        .ThenInclude(c => c.Coach)
                .Include(od => od.Package)
                    .ThenInclude(p => p.Media)
                .Include(od => od.Package)
                    .ThenInclude(p => p.Reviews.Where(r => r.TraineeId == traineeId))
                .Include(od => od.TrainingPlans)
                .Include(od => od.MealPlans)
                .Where(od => od.Order.BuyerId == traineeId
                             && od.Order.OrderStatus != null
                             && paidStatuses.Contains(od.Order.OrderStatus.ToUpper()));

            var totalCount = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(od => od.CreatedAt)
                .ThenByDescending(od => od.OrderDetailsId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
    }
}
