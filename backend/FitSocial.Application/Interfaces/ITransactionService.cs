using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Transactions;
using System;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface ITransactionService
{
    Task<PagedResultDto<TransactionHistoryDto>> GetMyTransactionsAsync(Guid userId, string userRole, TransactionFilterDto filter);
}
