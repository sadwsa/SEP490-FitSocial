using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Finance;
using FitSocial.Application.Exceptions;
using FitSocial.Domain.Interfaces;

namespace FitSocial.Application.Interfaces;

public interface IMoneyMovementService
{
    Task<ApiResponseDto<MoneyMovementListDto>> GetMoneyMovementsAsync(
        MoneyMovementQueryDto query,
        CancellationToken ct = default);
}