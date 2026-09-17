using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.Coaches;
using FitSocial.Client.Services.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.Coaches;

public class CoachService : ICoachService
{
    private readonly ApiClient _apiClient;
    private const string BaseEndpoint = "coaches";

    public CoachService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResponse<IEnumerable<CoachListDto>>> GetAllCoachesAsync()
    {
        return await _apiClient.GetAsync<IEnumerable<CoachListDto>>(BaseEndpoint);
    }
}
