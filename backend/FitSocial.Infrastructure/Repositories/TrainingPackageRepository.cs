using FitSocial.Domain.Constants;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Domain.Policies;
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
        private static readonly string[] PaidPurchasedStatuses =
        {
            PaymentConstants.OrderStatusPaid,
            PaymentConstants.OrderStatusActive,
            PaymentConstants.OrderStatusCompleted,
            PaymentConstants.OrderStatusRefundPending,
            PaymentConstants.OrderStatusRefunded
        };

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

        public Task<(List<OrderDetail> Items, int TotalCount)> GetPurchasedOrderDetailsAsync(
            Guid traineeId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return GetPurchasedOrderDetailsAsync(
                traineeId,
                pageNumber,
                pageSize,
                searchKeyword: null,
                packageType: null,
                status: null,
                sortBy: null,
                cancellationToken: cancellationToken);
        }

        public async Task<(List<OrderDetail> Items, int TotalCount)> GetPurchasedOrderDetailsAsync(
            Guid traineeId,
            int pageNumber,
            int pageSize,
            string? searchKeyword,
            string? packageType = null,
            string? status = null,
            string? sortBy = null,
            CancellationToken cancellationToken = default)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = DbContext.Set<OrderDetail>()
                .Include(od => od.Order)
                    .ThenInclude(o => o.RefundRequests)
                .Include(od => od.Package)
                    .ThenInclude(p => p!.Coach)
                        .ThenInclude(c => c.Coach)
                .Include(od => od.Package)
                    .ThenInclude(p => p!.Coach)
                        .ThenInclude(c => c.CoachCertificates)
                .Include(od => od.Package)
                    .ThenInclude(p => p!.Media)
                .Include(od => od.Package)
                    .ThenInclude(p => p!.Reviews.Where(r => r.TraineeId == traineeId))
                        .ThenInclude(r => r.ReviewMedia)
                .Include(od => od.TrainingPlans)
                .Include(od => od.MealPlans)
                .Where(od => od.PackageId != null
                             && od.Package != null
                             && od.Order.BuyerId == traineeId
                             && (od.Order.OrderType == null || od.Order.OrderType == PaymentConstants.OrderTypePackage)
                             && od.Order.OrderStatus != null
                             && PaidPurchasedStatuses.Contains(od.Order.OrderStatus.ToUpper()));

            if (!string.IsNullOrWhiteSpace(searchKeyword))
            {
                var term = searchKeyword.Trim().ToLower();
                query = query.Where(od =>
                    (od.PackageTitle != null && od.PackageTitle.ToLower().Contains(term)) ||
                    (od.Package != null && od.Package.Title != null && od.Package.Title.ToLower().Contains(term)) ||
                    (od.CoachName != null && od.CoachName.ToLower().Contains(term)) ||
                    (od.Package != null && od.Package.Coach != null && od.Package.Coach.Coach != null &&
                     od.Package.Coach.Coach.FullName != null && od.Package.Coach.Coach.FullName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(packageType) && !string.Equals(packageType.Trim(), "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedType = packageType.Trim().ToUpperInvariant();
                query = query.Where(od =>
                    ((od.PackageType ?? (od.Package != null ? od.Package.PackageType : null) ?? "BOTH").ToUpper()) == normalizedType);
            }

            var hasStatusFilter = !string.IsNullOrWhiteSpace(status) && !string.Equals(status.Trim(), "ALL", StringComparison.OrdinalIgnoreCase);
            var normalizedSort = string.IsNullOrWhiteSpace(sortBy) ? "NEWEST" : sortBy.Trim().ToUpperInvariant();
            var requiresInMemoryEvaluation = hasStatusFilter || normalizedSort == "EXPIRING_SOON";

            if (!requiresInMemoryEvaluation)
            {
                var totalSqlCount = await query.CountAsync(cancellationToken);
                var orderedSql = normalizedSort switch
                {
                    "OLDEST" => query
                        .OrderBy(od => od.Order.CreatedAt ?? od.CreatedAt ?? DateTime.MinValue)
                        .ThenBy(od => od.OrderDetailsId),
                    "PRICE_DESC" => query
                        .OrderByDescending(od => od.PackagePrice ?? (od.Package != null ? od.Package.Price : 0m))
                        .ThenByDescending(od => od.CreatedAt),
                    "PRICE_ASC" => query
                        .OrderBy(od => od.PackagePrice ?? (od.Package != null ? od.Package.Price : 0m))
                        .ThenByDescending(od => od.CreatedAt),
                    _ => query
                        .OrderByDescending(od => od.Order.CreatedAt ?? od.CreatedAt ?? DateTime.MinValue)
                        .ThenByDescending(od => od.OrderDetailsId)
                };

                var pagedItems = await orderedSql
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);

                return (pagedItems, totalSqlCount);
            }

            var allMatching = await query
                .OrderByDescending(od => od.CreatedAt)
                .ThenByDescending(od => od.OrderDetailsId)
                .AsNoTracking()
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var nowUtc = DateTime.UtcNow;
            var projected = allMatching
                .Select(od => new
                {
                    OrderDetail = od,
                    State = PurchasedPackageEnrollmentPolicy.Resolve(od, nowUtc)
                });

            if (hasStatusFilter)
            {
                var normalizedStatus = status!.Trim().ToUpperInvariant();
                projected = projected.Where(x => string.Equals(x.State.EnrollmentStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase));
            }

            projected = normalizedSort switch
            {
                "OLDEST" => projected
                    .OrderBy(x => x.OrderDetail.Order?.CreatedAt ?? x.OrderDetail.CreatedAt ?? DateTime.MinValue)
                    .ThenBy(x => x.OrderDetail.OrderDetailsId),
                "EXPIRING_SOON" => projected
                    .OrderBy(x => x.State.EnrollmentStatus == "ACTIVE" ? 0 : 1)
                    .ThenBy(x => x.State.ExpiresAt ?? DateTime.MaxValue)
                    .ThenByDescending(x => x.OrderDetail.OrderDetailsId),
                "PRICE_DESC" => projected
                    .OrderByDescending(x => x.OrderDetail.PackagePrice ?? x.OrderDetail.Package?.Price ?? 0m)
                    .ThenByDescending(x => x.OrderDetail.CreatedAt),
                "PRICE_ASC" => projected
                    .OrderBy(x => x.OrderDetail.PackagePrice ?? x.OrderDetail.Package?.Price ?? 0m)
                    .ThenByDescending(x => x.OrderDetail.CreatedAt),
                _ => projected
                    .OrderByDescending(x => x.OrderDetail.Order?.CreatedAt ?? x.OrderDetail.CreatedAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.OrderDetail.OrderDetailsId)
            };

            var materialized = projected.Select(x => x.OrderDetail).ToList();
            var totalCount = materialized.Count;
            var items = materialized
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (items, totalCount);
        }
    }
}
