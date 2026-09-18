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
                Price = dto.Price,
                DurationDays = dto.DurationDays,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(package);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(package);
        }

        private static TrainingPackageResponseDto MapToDto(TrainingPackage entity)
        {
            return new TrainingPackageResponseDto
            {
                PackageId = entity.PackageId,
                CoachId = entity.CoachId,
                // Truy cập chuẩn xác theo DB: TrainingPackage -> CoachProfile -> User -> FullName
                CoachName = entity.Coach?.Coach?.FullName ?? "Unknown Coach",
                Title = entity.Title,
                Price = entity.Price,
                DurationDays = entity.DurationDays,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt
            };
        }

        public Task<IEnumerable<TrainingPackageResponseDto>> GetAllPackagesAsync()
        {
            throw new NotImplementedException();
        }
    }
}
