using System.Threading.Tasks;
using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.SystemLogs;

namespace FitSocial.Client.Services.SystemLogs;

public interface ISystemLogService
{
    Task<ApiResponse<PagedResult<SystemLogItemModel>>> GetSystemLogsAsync(GetSystemLogsRequest request);
    Task<ApiResponse<SystemLogItemModel>> GetLogDetailAsync(long id);
}
