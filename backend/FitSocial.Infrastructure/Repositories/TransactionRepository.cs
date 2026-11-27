using FitSocial.Domain.Constants;
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
    public class TransactionRepository : ITransactionRepository
    {
        private readonly FitSocialDbContext _dbContext;

        private static readonly string[] PaidOrderStatuses =
        {
            PaymentConstants.OrderStatusPaid,
            PaymentConstants.OrderStatusCompleted,
            PaymentConstants.OrderStatusActive,
            PaymentConstants.OrderStatusRefunded,
            PaymentConstants.OrderStatusRefundPending
        };

        public TransactionRepository(FitSocialDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<(IEnumerable<TransactionRecord> Items, int TotalCount)> GetUserTransactionsAsync(
            Guid userId, string userRole, DateTime? fromDate, DateTime? toDate, string? type, int pageIndex, int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var isCoach = string.Equals(userRole, "COACH", StringComparison.OrdinalIgnoreCase);
            var normalizedType = type?.Trim().ToUpperInvariant();
            var isAllTypes = string.IsNullOrEmpty(normalizedType) || normalizedType == "ALL";
            var includeOrders = isAllTypes || normalizedType == "PACKAGE_PURCHASE" || normalizedType == "COACH_ACTIVATION";
            var includeRefunds = isAllTypes || normalizedType == "REFUND";
            var includePayouts = isCoach && (isAllTypes || normalizedType == "PAYOUT");

            var records = new List<TransactionRecord>();

            // Chuẩn hóa mốc thời gian lọc theo múi giờ Việt Nam (UTC+7) sang UTC
            DateTime? startUtc = fromDate.HasValue ? fromDate.Value.Date.AddHours(-7) : null;
            DateTime? endUtc = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1).AddHours(-7) : null;

            // ================================================================
            // 1. GIAO DỊCH ĐƠN HÀNG (PACKAGE + COACH_ACTIVATION)
            // ================================================================
            if (includeOrders)
            {
                var orderQuery = _dbContext.Set<Order>()
                    .AsNoTracking()
                    .AsSplitQuery()
                    .Include(o => o.Payments)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Package)
                    .Where(o => o.OrderStatus != null && PaidOrderStatuses.Contains(o.OrderStatus.ToUpper()));

                if (isCoach)
                {
                    orderQuery = orderQuery.Where(o =>
                        o.CoachId == userId ||
                        o.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == userId) ||
                        o.BuyerId == userId);
                }
                else
                {
                    orderQuery = orderQuery.Where(o => o.BuyerId == userId);
                }

                if (normalizedType == "COACH_ACTIVATION")
                {
                    orderQuery = orderQuery.Where(o => o.OrderType == PaymentConstants.OrderTypeCoachActivation);
                }
                else if (normalizedType == "PACKAGE_PURCHASE")
                {
                    orderQuery = orderQuery.Where(o => o.OrderType != PaymentConstants.OrderTypeCoachActivation);
                }

                if (startUtc.HasValue)
                    orderQuery = orderQuery.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value >= startUtc.Value);
                if (endUtc.HasValue)
                    orderQuery = orderQuery.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value <= endUtc.Value);

                var orders = await orderQuery.ToListAsync(cancellationToken);
                foreach (var order in orders)
                {
                    var payment = order.Payments?.FirstOrDefault(p => p.Status == "SUCCESS")
                               ?? order.Payments?.FirstOrDefault();
                    var packageTitle = order.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";
                    var isCoachSelling = isCoach && order.BuyerId != userId && (order.CoachId == userId || (order.OrderDetails != null && order.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == userId)));

                    if (order.OrderType == PaymentConstants.OrderTypeCoachActivation)
                    {
                        records.Add(new TransactionRecord
                        {
                            TransactionId = payment?.PaymentId ?? order.OrderId,
                            Date = order.CreatedAt ?? DateTime.UtcNow,
                            Amount = -(order.TotalAmount ?? 0),
                            Currency = "VND",
                            Type = "COACH_ACTIVATION",
                            Status = order.OrderStatus ?? "COMPLETED",
                            Description = "Coach subscription activation / renewal",
                            ReferenceId = payment?.GatewayTransactionId ?? order.OrderId.ToString()
                        });
                    }
                    else
                    {
                        records.Add(new TransactionRecord
                        {
                            TransactionId = payment?.PaymentId ?? order.OrderId,
                            Date = order.CreatedAt ?? DateTime.UtcNow,
                            Amount = isCoachSelling ? (order.TotalAmount ?? 0) : -(order.TotalAmount ?? 0),
                            Currency = "VND",
                            Type = "PACKAGE_PURCHASE",
                            Status = order.OrderStatus ?? "COMPLETED",
                            Description = isCoachSelling
                                ? $"Package sold: {packageTitle}"
                                : $"Package purchased: {packageTitle}",
                            ReferenceId = payment?.GatewayTransactionId ?? order.OrderId.ToString()
                        });
                    }
                }
            }

            // ================================================================
            // 2. HOÀN TRẢ TIỀN (REFUND)
            // ================================================================
            if (includeRefunds)
            {
                var refundQuery = _dbContext.Set<RefundRequest>()
                    .AsNoTracking()
                    .Include(r => r.Order)
                        .ThenInclude(od => od.OrderDetails)
                            .ThenInclude(od => od.Package)
                    .Where(r => r.Status == "APPROVED" || r.Status == "REFUNDED" || r.Status == "PENDING");

                if (isCoach)
                {
                    refundQuery = refundQuery.Where(r => r.RequestedBy == userId ||
                        (r.Order != null && (r.Order.CoachId == userId || r.Order.OrderDetails.Any(od => od.Package != null && od.Package.CoachId == userId))));
                }
                else
                {
                    refundQuery = refundQuery.Where(r => r.RequestedBy == userId);
                }

                if (startUtc.HasValue)
                {
                    refundQuery = refundQuery.Where(r => (r.RefundedAt ?? r.RequestedAt) >= startUtc.Value);
                }
                if (endUtc.HasValue)
                {
                    refundQuery = refundQuery.Where(r => (r.RefundedAt ?? r.RequestedAt) <= endUtc.Value);
                }

                var refunds = await refundQuery.ToListAsync(cancellationToken);
                foreach (var refund in refunds)
                {
                    var packageTitle = refund.Order?.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";
                    var rawAmount = refund.ApprovedAmount ?? refund.RequestedAmount ?? 0;
                    var isBuyerRefund = refund.RequestedBy == userId;
                    var amount = isBuyerRefund ? rawAmount : -rawAmount;

                    records.Add(new TransactionRecord
                    {
                        TransactionId = refund.RefundRequestId,
                        Date = refund.RefundedAt ?? refund.RequestedAt ?? DateTime.UtcNow,
                        Amount = amount,
                        Currency = "VND",
                        Type = "REFUND",
                        Status = refund.Status ?? "PENDING",
                        Description = isBuyerRefund
                            ? $"Refund received: {packageTitle}"
                            : $"Refund deducted: {packageTitle}",
                        ReferenceId = refund.RefundTransactionRef ?? refund.OrderId.ToString()
                    });
                }
            }

            // ================================================================
            // 3. GIẢI NGÂN HỆ THỐNG (PAYOUT) - Dành riêng cho Coach
            // ================================================================
            if (includePayouts)
            {
                var payoutQuery = _dbContext.Set<Payout>()
                    .AsNoTracking()
                    .Include(p => p.PayoutItems)
                        .ThenInclude(pi => pi.Order)
                            .ThenInclude(o => o.OrderDetails)
                    .Where(p => p.CoachId == userId && (p.Status == "PROCESSED" || p.Status == "COMPLETED" || p.Status == "PENDING"));

                if (startUtc.HasValue)
                    payoutQuery = payoutQuery.Where(p => (p.ProcessedAt ?? p.CreatedAt) >= startUtc.Value);
                if (endUtc.HasValue)
                    payoutQuery = payoutQuery.Where(p => (p.ProcessedAt ?? p.CreatedAt) <= endUtc.Value);

                var payouts = await payoutQuery.ToListAsync(cancellationToken);
                foreach (var payout in payouts)
                {
                    var totalNetAmount = payout.PayoutItems?.Sum(pi => pi.NetAmount ?? 0) ?? 0;
                    var packageTitle = payout.PayoutItems?
                        .FirstOrDefault(pi => pi.Order != null && pi.Order.OrderDetails != null && pi.Order.OrderDetails.Any())
                        ?.Order?.OrderDetails?.FirstOrDefault()?.PackageTitle;

                    var desc = !string.IsNullOrEmpty(packageTitle)
                        ? $"System payout: {packageTitle} (Cycle {payout.PayoutMonth:D2}/{payout.PayoutYear})"
                        : $"System payout (Cycle {payout.PayoutMonth:D2}/{payout.PayoutYear})";

                    records.Add(new TransactionRecord
                    {
                        TransactionId = payout.PayoutId,
                        Date = payout.ProcessedAt ?? payout.CreatedAt ?? DateTime.UtcNow,
                        Amount = payout.NetPayoutAmount ?? totalNetAmount,
                        Currency = "VND",
                        Type = "PAYOUT",
                        Status = payout.Status ?? "PROCESSED",
                        Description = desc,
                        ReferenceId = payout.TransactionRef ?? $"PAYOUT-{payout.PayoutId}"
                    });
                }
            }

            // ================================================================
            // 4. LỌC THEO TYPE & SẮP XẾP, PHÂN TRANG
            // ================================================================
            if (!isAllTypes)
            {
                records = records.Where(r => r.Type != null && r.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = records.Count;
            var pagedItems = records
                .OrderByDescending(x => x.Date)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedItems, totalCount);
        }

        public async Task<(IEnumerable<TransactionRecord> Items, int TotalCount)> GetCoachPayoutHistoryAsync(
            Guid coachId, DateTime? fromDate, DateTime? toDate, string? type, int pageIndex, int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            DateTime? startUtc = fromDate.HasValue ? fromDate.Value.Date.AddHours(-7) : null;
            DateTime? endUtc = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddTicks(-1).AddHours(-7) : null;

            var query = _dbContext.Set<Payout>()
                .AsNoTracking()
                .Include(p => p.PayoutItems)
                    .ThenInclude(pi => pi.Order)
                        .ThenInclude(o => o.OrderDetails)
                .Where(p => p.CoachId == coachId
                         && (p.Status == "PROCESSED" || p.Status == "COMPLETED" || p.Status == "PENDING"));

            if (startUtc.HasValue)
                query = query.Where(p => (p.ProcessedAt.HasValue && p.ProcessedAt.Value >= startUtc.Value) || (p.CreatedAt.HasValue && p.CreatedAt.Value >= startUtc.Value));
            if (endUtc.HasValue)
                query = query.Where(p => (p.ProcessedAt.HasValue && p.ProcessedAt.Value <= endUtc.Value) || (p.CreatedAt.HasValue && p.CreatedAt.Value <= endUtc.Value));

            var rawPayouts = await query.ToListAsync(cancellationToken);
            var records = new List<TransactionRecord>();

            foreach (var payout in rawPayouts)
            {
                var totalNetAmount = payout.PayoutItems?.Sum(pi => pi.NetAmount ?? 0) ?? 0;
                var packageTitle = payout.PayoutItems?
                    .FirstOrDefault(pi => pi.Order != null && pi.Order.OrderDetails != null && pi.Order.OrderDetails.Any())
                    ?.Order?.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";

                records.Add(new TransactionRecord
                {
                    TransactionId = payout.PayoutId,
                    Date = payout.ProcessedAt ?? payout.CreatedAt ?? DateTime.UtcNow,
                    Amount = payout.NetPayoutAmount ?? totalNetAmount,
                    Currency = "VND",
                    Type = "PAYOUT",
                    Status = payout.Status ?? "PROCESSED",
                    Description = $"System payout: {packageTitle}",
                    ReferenceId = payout.TransactionRef ?? $"PAYOUT-{payout.PayoutId}"
                });
            }

            if (!string.IsNullOrWhiteSpace(type) && !type.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                records = records.Where(r => r.Type != null && r.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = records.Count;
            var pagedItems = records
                .OrderByDescending(x => x.Date)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedItems, totalCount);
        }
    }
}
