using FitSocial.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<(IEnumerable<TransactionRecord> Items, int TotalCount)> GetUserTransactionsAsync(
        Guid userId, string userRole, DateTime? fromDate, DateTime? toDate, string? type, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
}
