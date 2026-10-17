using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Client.Models.Appeals;
using FitSocial.Client.Models.Common;

namespace FitSocial.Client.Services.Appeals;

public interface IAppealService
{
    Task<ApiResponse<List<AppealableReport>>> GetMyAppealableReportsAsync(string? appealToken = null);
    Task<ApiResponse<AppealableReport>> GetAppealableReportByIdAsync(Guid reportId, string? appealToken = null);
    Task<ApiResponse<AppealDetail>> SubmitAppealAsync(SubmitAppealRequest request);
    Task<ApiResponse<AppealDetail>> GetAppealByIdAsync(Guid appealId, string? appealToken = null);
}
