using FitSocial.Application.DTOs.Coach;
using FitSocial.Application.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface ICoachBankAccountService
{
    Task<ApiResponseDto<List<CoachBankAccountDto>>> GetAccountsAsync(Guid coachId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<CoachBankAccountDto>> AddAccountAsync(Guid coachId, CreateCoachBankAccountDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> SetDefaultAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<bool>> DeleteAccountAsync(Guid coachId, Guid bankId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<string?>> LookupAccountNameAsync(string bin, string accountNumber, CancellationToken cancellationToken = default);
}
