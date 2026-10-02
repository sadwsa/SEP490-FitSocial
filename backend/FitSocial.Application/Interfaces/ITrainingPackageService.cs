using FitSocial.Application.DTOs.TrainingPackage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitSocial.Application.Interfaces
{
    public interface ITrainingPackageService
    {
        Task<TrainingPackageResponseDto?> GetPackageByIdAsync(Guid id);
        Task<bool> SoftDeletePackageAsync(Guid id, Guid currentUserId);
        Task<TrainingPackageResponseDto> CreatePackageAsync(Guid currentUserId, CreateTrainingPackageDto dto);
        Task<TrainingPackageResponseDto?> UpdatePackageAsync(Guid id, Guid currentUserId, UpdateTrainingPackageDto dto);
        Task<IEnumerable<TrainingPackageResponseDto>> GetMyPackagesAsync(Guid currentUserId);
        Task<IEnumerable<TrainingPackageResponseDto>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null);
        Task<IEnumerable<TrainingPackageResponseDto>> GetPurchasedPackagesAsync(Guid currentUserId);
    }
}
