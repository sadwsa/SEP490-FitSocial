using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Coaches;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Coaches;

public interface ICoachService
{
    Task<ApiResponse<IEnumerable<CoachListDto>>> GetAllCoachesAsync();
}
