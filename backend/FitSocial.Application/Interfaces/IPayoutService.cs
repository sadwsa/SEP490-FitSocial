using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Payouts;

namespace FitSocial.Application.Interfaces;

public interface IPayoutService
{
    Task<ApiResponseDto<List<PayoutHistoryItemDto>>> GetPayoutHistoryAsync(int? month, int? year, string? status, Guid? coachId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<GeneratePayoutsResultDto>> GenerateMonthlyPayoutsAsync(int month, int year, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<PayoutDetailDto>> GetPayoutDetailAsync(Guid payoutId, CancellationToken cancellationToken = default);
    Task<ApiResponseDto<ProcessPayoutResultDto>> ProcessPayoutAsync(Guid payoutId, Guid staffId, string? ipAddress = null, CancellationToken cancellationToken = default);
}
