using FitSocial.Application.DTOs.Coach;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces;

public interface ICoachService
{
    Task<IEnumerable<CoachListDto>> GetAllCoachesAsync(string? searchKeyword = null, int? minExperience = null, string? sortBy = null);
}
