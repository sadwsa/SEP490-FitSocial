using System;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.SystemLogs;

namespace FitSocial.Application.Interfaces;

public interface ISystemLogService
{
    Task<ApiResponseDto<PagedResultDto<SystemLogItemDto>>> GetSystemLogsAsync(
        GetSystemLogsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<SystemLogSummaryDto>> GetSystemLogsSummaryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<SystemLogItemDto>> GetLogDetailAsync(
        long logId,
        CancellationToken cancellationToken = default);
}
