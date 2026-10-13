using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using FitSocial.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly FitSocialDbContext _dbContext;

    public TransactionRepository(FitSocialDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IEnumerable<TransactionRecord> Items, int TotalCount)> GetUserTransactionsAsync(
        Guid userId, string userRole, DateTime? fromDate, DateTime? toDate, string? type, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var records = new List<TransactionRecord>();

        // CHỈ LẤY GIAO DỊCH LÀ COACH_ACTIVATION THEO YÊU CẦU CỦA BẠN
        var coachActivationOrders = await _dbContext.Set<Order>()
            .Include(o => o.Payments)
            .Where(o => o.BuyerId == userId
                     && o.OrderType == FitSocial.Domain.Constants.PaymentConstants.OrderTypeCoachActivation
                     && (o.OrderStatus == "COMPLETED"))
            .ToListAsync(cancellationToken);

        foreach (var order in coachActivationOrders)
        {
            var payment = order.Payments.FirstOrDefault(p => p.Status == "SUCCESS");
            records.Add(new TransactionRecord
            {
                TransactionId = payment?.PaymentId ?? order.OrderId,
                Date = order.CreatedAt ?? DateTime.UtcNow,
                Amount = -(order.TotalAmount ?? 0), // Số âm (vì là Trừ tiền)
                Currency = "VND",
                Type = "COACH_ACTIVATION",
                Status = order.OrderStatus ?? "COMPLETED",
                Description = "Coach Account Activation Fee",
                ReferenceId = payment?.GatewayTransactionId ?? order.OrderId.ToString()
            });
        }

        // Lọc theo ngày
        if (fromDate.HasValue) records = records.Where(x => x.Date >= fromDate.Value).ToList();
        if (toDate.HasValue) records = records.Where(x => x.Date <= toDate.Value).ToList();

        // Phân trang
        var totalCount = records.Count;
        var pagedItems = records.OrderByDescending(x => x.Date)
                                .Skip((pageIndex - 1) * pageSize)
                                .Take(pageSize)
                                .ToList();

        return (pagedItems, totalCount);
    }
}
