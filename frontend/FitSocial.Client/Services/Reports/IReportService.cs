using System;
using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Reports;

namespace FitSocial.Client.Services.Reports;

public interface IReportService
{
    Task<ApiResponse<PagedResult<ReportListItemDto>>> GetReportsAsync(
        string? searchTerm = null,
        string? status = null,
        string? targetType = null,
        Guid? reporterId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 10);
}
