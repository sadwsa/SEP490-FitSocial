using FitSocial.Application.DTOs.Common;
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


        // --- CÁC PHƯƠNG THỨC REVIEWS & REPLY GÓI TẬP ---
        Task<ApiResponseDto<PackageReviewSummaryDto>> GetPackageReviewsAsync(Guid packageId, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<bool>> SubmitPackageReviewAsync(Guid packageId, Guid traineeId, CreatePackageReviewDto dto, CancellationToken cancellationToken = default);
        Task<ApiResponseDto<bool>> ReplyToReviewAsync(Guid packageId, Guid reviewId, Guid coachId, ReplyReviewDto dto, CancellationToken cancellationToken = default);


    }
}
