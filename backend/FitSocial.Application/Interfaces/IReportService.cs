using System.Threading;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Common;
using FitSocial.Application.DTOs.Reports;

namespace FitSocial.Application.Interfaces;

public interface IReportService
{
    Task<ApiResponseDto<PagedResultDto<ReportListItemDto>>> GetReportsAsync(
        GetReportsQueryDto query,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<ReportListItemDto>> GetReportByIdAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<ReportListItemDto>> ResolveReportAsync(
        Guid reportId,
        Guid staffId,
        ResolveReportRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<ReportListItemDto>> ProcessViolationReportAsync(
        Guid reportId,
        Guid staffId,
        ProcessViolationReportRequestDto request,
        CancellationToken cancellationToken = default);


    Task<ApiResponseDto<ReportListItemDto>> SubmitAppealAsync(
        Guid reportId,
        Guid userId,
        SubmitAppealRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<ReportListItemDto>> ReviewAppealAsync(
        Guid reportId,
        Guid staffId,
        ReviewAppealRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ApiResponseDto<ReportListItemDto>> ReportUserAsync(
        Guid reporterId,
        CreateUserReportRequestDto request,
        CancellationToken cancellationToken = default);
}
