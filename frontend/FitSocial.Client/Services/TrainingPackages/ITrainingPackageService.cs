using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPackages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.TrainingPackages;

public interface ITrainingPackageService
{
    Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetMyPackagesAsync();
 Task<ApiResponse<bool>> DeletePackageAsync(Guid id);
    Task<ApiResponse<TrainingPackageResponseDto>> GetPackageByIdAsync(Guid id);
    Task<ApiResponse<TrainingPackageResponseDto>> CreatePackageAsync(CreateTrainingPackageDto dto);
    Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetPackagesByCoachIdAsync(Guid coachId);
    Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null);
}
