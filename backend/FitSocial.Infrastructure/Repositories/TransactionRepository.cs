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

            var isCoach = string.Equals(userRole, "COACH", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(userRole, "Coach", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(userRole, "coach", StringComparison.OrdinalIgnoreCase);

            var records = new List<TransactionRecord>();

            var paidStatuses = new[]
            {
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusPaid,
                "PAID",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusCompleted,
                "COMPLETED",
                FitSocial.Domain.Constants.PaymentConstants.OrderStatusActive,
                "ACTIVE"
            };

            // ================================================================
            // 1. GIAO DỊCH MUA/BÁN GÓI TẬP (PACKAGE) - user với user
            // ================================================================
            var orderQuery = _dbContext.Set<Order>()
                .Include(o => o.Payments)
                .Include(o => o.OrderDetails)
                .Where(o => o.OrderType == FitSocial.Domain.Constants.PaymentConstants.OrderTypePackage);

            if (isCoach)
            {
                orderQuery = orderQuery.Where(o => o.CoachId.HasValue && o.CoachId.Value == userId);
            }
            else
            {
                orderQuery = orderQuery.Where(o => o.BuyerId == userId);
            }

            orderQuery = orderQuery.Where(o => o.OrderStatus != null && paidStatuses.Contains(o.OrderStatus.ToUpper()));

            if (fromDate.HasValue)
                orderQuery = orderQuery.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value >= fromDate.Value);
            if (toDate.HasValue)
                orderQuery = orderQuery.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value <= toDate.Value);

            foreach (var order in await orderQuery.ToListAsync(cancellationToken))
            {
                var payment = order.Payments?.FirstOrDefault(p => p.Status == "SUCCESS");
                var packageTitle = order.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";

                records.Add(new TransactionRecord
                {
                    TransactionId = payment?.PaymentId ?? order.OrderId,
                    Date = order.CreatedAt ?? DateTime.UtcNow,
                    Amount = isCoach ? (order.TotalAmount ?? 0) : -(order.TotalAmount ?? 0),
                    Currency = "VND",
                    Type = "PACKAGE_PURCHASE",
                    Status = order.OrderStatus ?? "COMPLETED",
                    Description = isCoach
                        ? $"Package sold: {packageTitle}"
                        : $"Package purchased: {packageTitle}",
                    ReferenceId = payment?.GatewayTransactionId ?? order.OrderId.ToString()
                });
            }

            // ================================================================
            // 2. HOÀN TRẢ TIỀN (REFUND) - user với user
            //    Trainee (RequestedBy): nhận tiền lại (dương)
            //    Coach (Order.CoachId): mất tiền (âm)
            // ================================================================
            var refundQuery = _dbContext.Set<RefundRequest>()
                .Include(r => r.Order)
                    .ThenInclude(od => od.OrderDetails)
                .Include(r => r.Payment)
                .Where(r => r.Status == "APPROVED" || r.Status == "REFUNDED" || r.Status == "PENDING");

            if (isCoach)
            {
                // Coach xem refund của đơn mình bán (mất tiền)
                refundQuery = refundQuery.Where(r => r.Order.CoachId.HasValue && r.Order.CoachId.Value == userId);
            }
            else
            {
                // Trainee xem refund của đơn mình mua (nhận tiền)
                refundQuery = refundQuery.Where(r => r.RequestedBy == userId);
            }

            if (fromDate.HasValue)
                refundQuery = refundQuery.Where(r =>
                    (r.RefundedAt.HasValue && r.RefundedAt.Value >= fromDate.Value) ||
                    (r.RequestedAt.HasValue && r.RequestedAt.Value >= fromDate.Value));
            if (toDate.HasValue)
                refundQuery = refundQuery.Where(r =>
                    (r.RefundedAt.HasValue && r.RefundedAt.Value <= toDate.Value) ||
                    (r.RequestedAt.HasValue && r.RequestedAt.Value <= toDate.Value));

            foreach (var refund in await refundQuery.ToListAsync(cancellationToken))
            {
                var packageTitle = refund.Order?.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";

                // Trainee nhận tiền (dương), Coach mất tiền (âm)
                var amount = isCoach ? -(refund.ApprovedAmount ?? refund.RequestedAmount ?? 0)
                                     : (refund.ApprovedAmount ?? refund.RequestedAmount ?? 0);

                records.Add(new TransactionRecord
                {
                    TransactionId = refund.RefundRequestId,
                    Date = refund.RefundedAt ?? refund.RequestedAt ?? DateTime.UtcNow,
                    Amount = amount,
                    Currency = "VND",
                    Type = "REFUND",
                    Status = refund.Status ?? "PENDING",
                    Description = isCoach
                        ? $"Refund deducted: {packageTitle}"
                        : $"Refund received: {packageTitle}",
                    ReferenceId = refund.RefundTransactionRef ?? refund.OrderId.ToString()
                });
            }

            // ================================================================
            // Lọc theo type (nếu FE truyền)
            // ================================================================
            if (!string.IsNullOrWhiteSpace(type))
            {
                records = records.Where(r => r.Type != null && r.Type.ToUpper() == type.ToUpper()).ToList();
            }

            // Sắp xếp và phân trang
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

            // Chỉ lấy tiền hệ thống gửi coach (payout), KHÔNG có refund deducted
            var query = _dbContext.Set<Payout>()
                .Include(p => p.PayoutItems)
                    .ThenInclude(pi => pi.Order)
                        .ThenInclude(o => o.OrderDetails)
                .Where(p => p.CoachId == coachId
                         && (p.Status == "PROCESSED" || p.Status == "COMPLETED"));

            if (fromDate.HasValue)
                query = query.Where(p => p.ProcessedAt.HasValue && p.ProcessedAt.Value >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(p => p.ProcessedAt.HasValue && p.ProcessedAt.Value <= toDate.Value);

            var payouts = await query
                .OrderByDescending(p => p.ProcessedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var totalCount = await query.CountAsync(cancellationToken);

            var records = new List<TransactionRecord>();
            foreach (var payout in payouts)
            {
                // Lấy tổng net amount của tất cả items trong payout này
                var totalNetAmount = payout.PayoutItems?.Sum(pi => pi.NetAmount ?? 0) ?? 0;

                // Lấy package title từ item đầu tiên (nếu có)
                var packageTitle = payout.PayoutItems?
                    .FirstOrDefault(pi => pi.Order != null && pi.Order.OrderDetails != null && pi.Order.OrderDetails.Any())
                    ?.Order?.OrderDetails?.FirstOrDefault()?.PackageTitle ?? "Training Package";

                records.Add(new TransactionRecord
                {
                    TransactionId = payout.PayoutId,
                    Date = payout.ProcessedAt ?? payout.CreatedAt ?? DateTime.UtcNow,
                    Amount = payout.NetPayoutAmount ?? totalNetAmount, // Ưu tiên netpayoutamount từ bảng payouts
                    Currency = "VND",
                    Type = "PAYOUT",
                    Status = payout.Status ?? "PROCESSED",
                    Description = $"System payout: {packageTitle}",
                    ReferenceId = payout.TransactionRef ?? $"PAYOUT-{payout.PayoutId}"
                });
            }

            // Lọc theo type (nếu FE truyền)
            if (!string.IsNullOrWhiteSpace(type))
            {
                records = records.Where(r => r.Type != null && r.Type.ToUpper() == type.ToUpper()).ToList();
            }

            return (records, totalCount);
        }
    }
}
