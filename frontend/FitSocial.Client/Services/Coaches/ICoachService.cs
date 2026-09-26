using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Coaches;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Coaches;

public interface ICoachService
{
    Task<ApiResponse<IEnumerable<CoachListDto>>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null);
    Task<ApiResponse<List<TopCoachDto>>> GetTopCoachesAsync(int count = 5);
}
