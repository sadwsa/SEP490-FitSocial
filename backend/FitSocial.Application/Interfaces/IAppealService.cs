using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Appeals;
using FitSocial.Application.DTOs.Common;
using FitSocial.Domain.Entities;

namespace FitSocial.Application.Interfaces;

public interface IAppealService
{
    Task<ApiResponseDto<AppealDetailDto>> SubmitAppealAsync(
        Guid? currentUserId,
        SubmitAppealRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<List<AppealableReportDto>>> GetAppealableReportsAsync(
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AppealableReportDto>> GetReportForAppealAsync(
        Guid reportId,
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<AppealDetailDto>> GetAppealDetailAsync(
        Guid appealId,
        Guid? currentUserId,
        string? appealToken,
        CancellationToken cancellationToken = default);

    string GenerateAppealToken(User user);

    Guid ValidateAppealToken(string appealToken);
}
