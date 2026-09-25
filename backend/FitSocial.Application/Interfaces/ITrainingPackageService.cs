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
        Task<IEnumerable<TrainingPackageResponseDto>> GetMyPackagesAsync(Guid currentUserId);
    }
}
