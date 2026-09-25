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
}
