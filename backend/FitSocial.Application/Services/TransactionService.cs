using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Transactions;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FitSocial.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;

    public TransactionService(ITransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<TransactionHistoryDto>> GetMyTransactionsAsync(Guid userId, string userRole, TransactionFilterDto filter)
    {
        var (items, totalCount) = await _repository.GetUserTransactionsAsync(
            userId, userRole, filter.FromDate, filter.ToDate, filter.Type, filter.PageIndex, filter.PageSize);

        var dtos = items.Select(x => new TransactionHistoryDto
        {
            TransactionId = x.TransactionId,
            Date = x.Date,
            Amount = x.Amount,
            Currency = x.Currency,
            Type = x.Type,
            Status = x.Status,
            Description = x.Description,
            ReferenceId = x.ReferenceId
        }).ToList();

        return PagedResultDto<TransactionHistoryDto>.Create(dtos, totalCount, filter.PageIndex, filter.PageSize);
    }
}

