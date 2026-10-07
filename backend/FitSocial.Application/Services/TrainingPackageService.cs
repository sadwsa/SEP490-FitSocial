using FitSocial.Application.DTOs.TrainingPackage;
using FitSocial.Application.Interfaces;
using FitSocial.Domain.Entities;
using FitSocial.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FitSocial.Application.Services
{
    public class TrainingPackageService : ITrainingPackageService
    {
        private readonly ITrainingPackageRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public TrainingPackageService(ITrainingPackageRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetMyPackagesAsync(Guid currentUserId)
        {
            var packages = await _repository.GetPackagesByCoachIdAsync(currentUserId);
            return packages.Where(p => p.IsActive == true).Select(MapToDto);
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetAllPackagesAsync(string? searchKeyword = null, decimal? maxPrice = null, Guid? coachId = null)
        {
            var packages = await _repository.GetAllActivePackagesAsync(searchKeyword, maxPrice, coachId);
            return packages.Select(MapToDto);
        }

        public async Task<TrainingPackageResponseDto?> GetPackageByIdAsync(Guid id)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null)
            {
                return null;
            }
            return MapToDto(package);
        }

        public async Task<TrainingPackageResponseDto> CreatePackageAsync(Guid currentUserId, CreateTrainingPackageDto dto)
        {
            var package = new TrainingPackage
            {
                PackageId = Guid.NewGuid(),
                CoachId = currentUserId,
                Title = dto.Title,
                Description = dto.Description,
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                SessionCount = dto.SessionCount,
                MinAge = dto.MinAge,
                TargetAudience = dto.TargetAudience,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };

            if (dto.ImageUrls != null && dto.ImageUrls.Count > 0)
            {
                short sortOrder = 0;
                foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    package.Media.Add(new TrainingPackageMedium
                    {
                        MediaId = Guid.Empty,
                        PackageId = package.PackageId,
                        MediaUrl = url.Trim(),
                        MediaType = "IMAGE",
                        SortOrder = sortOrder++,
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                    });
                }
            }

            await _repository.AddAsync(package);
            await _unitOfWork.SaveChangesAsync();

            var createdPackage = await _repository.GetByIdAsync(package.PackageId);
            return MapToDto(createdPackage ?? package);
        }

        public async Task<TrainingPackageResponseDto?> UpdatePackageAsync(Guid id, Guid currentUserId, UpdateTrainingPackageDto dto)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null || package.CoachId != currentUserId)
            {
                return null;
            }

            if (dto.Title != null) package.Title = dto.Title;
            if (dto.Description != null) package.Description = dto.Description;
            if (dto.Price.HasValue) package.Price = dto.Price.Value;
            if (dto.DurationDays.HasValue) package.DurationDays = dto.DurationDays.Value;
            if (dto.SessionCount.HasValue) package.SessionCount = dto.SessionCount.Value;
            if (dto.MinAge.HasValue) package.MinAge = dto.MinAge.Value;
            if (dto.TargetAudience != null) package.TargetAudience = dto.TargetAudience;
            if (dto.IsActive.HasValue) package.IsActive = dto.IsActive.Value;

            if (dto.ImageUrls != null)
            {
                package.Media.Clear();
                short sortOrder = 0;
                foreach (var url in dto.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    package.Media.Add(new TrainingPackageMedium
                    {
                        MediaId = Guid.Empty,
                        PackageId = package.PackageId,
                        MediaUrl = url.Trim(),
                        MediaType = "IMAGE",
                        SortOrder = sortOrder++,
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return MapToDto(package);
        }

        public async Task<bool> SoftDeletePackageAsync(Guid id, Guid currentUserId)
        {
            var package = await _repository.GetByIdAsync(id);
            if (package == null || package.CoachId != currentUserId)
            {
                return false;
            }

            package.IsActive = false;
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<TrainingPackageResponseDto>> GetPurchasedPackagesAsync(Guid currentUserId)
        {
            var packages = await _repository.GetPurchasedPackagesAsync(currentUserId);
            return packages.Select(MapToDto);
        }

        private static TrainingPackageResponseDto MapToDto(TrainingPackage entity)
        {
            return new TrainingPackageResponseDto
            {
                PackageId = entity.PackageId,
                CoachId = entity.CoachId,
                CoachName = entity.Coach?.Coach?.FullName ?? "Unknown Coach",
                Title = entity.Title,
                Description = entity.Description,
                Price = entity.Price,
                DurationDays = entity.DurationDays,
                SessionCount = entity.SessionCount,
                MinAge = entity.MinAge,
                TargetAudience = entity.TargetAudience,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                Media = entity.Media?.OrderBy(m => m.SortOrder).Select(m => new TrainingPackageMediaDto
                {
                    MediaId = m.MediaId,
                    PackageId = m.PackageId,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType,
                    SortOrder = m.SortOrder,
                    CreatedAt = m.CreatedAt
                }).ToList() ?? new List<TrainingPackageMediaDto>()
            };
        }
    }
}
