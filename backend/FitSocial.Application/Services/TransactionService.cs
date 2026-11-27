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
        var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize < 1 ? 10 : Math.Min(filter.PageSize, 100);

        var (items, totalCount) = await _repository.GetUserTransactionsAsync(
            userId, userRole, filter.FromDate, filter.ToDate, filter.Type, pageIndex, pageSize);

        var dtos = items.Select(MapToDto).ToList();

        return PagedResultDto<TransactionHistoryDto>.Create(dtos, totalCount, pageIndex, pageSize);
    }

    public async Task<PagedResultDto<TransactionHistoryDto>> GetCoachPayoutHistoryAsync(Guid coachId, TransactionFilterDto filter)
    {
        filter ??= new TransactionFilterDto();
        var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize < 1 ? 10 : Math.Min(filter.PageSize, 100);

        var (items, totalCount) = await _repository.GetCoachPayoutHistoryAsync(
            coachId, filter.FromDate, filter.ToDate, filter.Type, pageIndex, pageSize);

        var dtos = items.Select(MapToDto).ToList();

        return PagedResultDto<TransactionHistoryDto>.Create(dtos, totalCount, pageIndex, pageSize);
    }

    private static TransactionHistoryDto MapToDto(FitSocial.Domain.Entities.TransactionRecord x) => new()
    {
        TransactionId = x.TransactionId,
        Date = x.Date,
        Amount = x.Amount,
        Currency = x.Currency,
        Type = x.Type,
        Status = x.Status,
        Description = x.Description,
        ReferenceId = x.ReferenceId
    };
}

