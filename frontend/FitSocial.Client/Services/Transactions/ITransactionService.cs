using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Transactions;

namespace FitSocial.Client.Services.Transactions;

public interface ITransactionService
{
    Task<PagedResult<TransactionHistoryDto>?> GetMyTransactionsAsync(TransactionFilterDto filter);
}
