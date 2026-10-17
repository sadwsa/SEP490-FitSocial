using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Finance;

namespace FitSocial.Client.Services.Finance;

public interface IMoneyMovementService
{
    Task<ApiResponse<MoneyMovementListDto>> GetMoneyMovementsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? movementType = null,
        string? recipientRole = null,
        string? status = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 20);
}