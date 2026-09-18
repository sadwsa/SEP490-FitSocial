using FitSocial.Client.Models.Common;
using FitSocial.Client.Models.TrainingPackages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Client.Services.TrainingPackages;

public interface ITrainingPackageService
{
    Task<ApiResponse<IEnumerable<TrainingPackageResponseDto>>> GetMyPackagesAsync();
    Task<ApiResponse<TrainingPackageResponseDto>> GetPackageByIdAsync(Guid id);
    Task<ApiResponse<TrainingPackageResponseDto>> CreatePackageAsync(CreateTrainingPackageDto dto);
}
